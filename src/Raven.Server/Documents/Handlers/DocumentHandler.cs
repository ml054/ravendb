﻿// -----------------------------------------------------------------------
//  <copyright file="DocumentHandler.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Raven.Client;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Server.Documents.Includes;
using Raven.Server.Documents.Patch;
using Raven.Server.Json;
using Raven.Server.NotificationCenter.Notifications.Details;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;
using Sparrow;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Base64 = Sparrow.Utils.Base64;
using ConcurrencyException = Voron.Exceptions.ConcurrencyException;
using DeleteDocumentCommand = Raven.Server.Documents.TransactionCommands.DeleteDocumentCommand;
using PatchRequest = Raven.Server.Documents.Patch.PatchRequest;

namespace Raven.Server.Documents.Handlers
{
    public class DocumentHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/docs", "HEAD", AuthorizationStatus.ValidUser)]
        public Task Head()
        {
            var id = GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");
            var changeVector = GetStringFromHeaders("If-None-Match");

            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                var document = Database.DocumentsStorage.Get(context, id);
                if (document == null)
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                else
                {
                    if (changeVector == document.ChangeVector)
                        HttpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                    else
                        HttpContext.Response.Headers[Constants.Headers.Etag] = "\"" + document.ChangeVector + "\"";
                }

                return Task.CompletedTask;
            }
        }

        [RavenAction("/databases/*/docs", "GET", AuthorizationStatus.ValidUser)]
        public Task Get()
        {
            var ids = GetStringValuesQueryString("id", required: false);
            var metadataOnly = GetBoolValueQueryString("metadata-only", required: false) ?? false;

            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                if (ids.Count > 0)
                    GetDocumentsById(context, ids, metadataOnly);
                else
                    GetDocuments(context, metadataOnly);

                return Task.CompletedTask;
            }
        }

        [RavenAction("/databases/*/docs", "POST", AuthorizationStatus.ValidUser)]
        public async Task PostGet()
        {
            var metadataOnly = GetBoolValueQueryString("metadata-only", required: false) ?? false;

            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            {
                var docs = await context.ReadForMemoryAsync(RequestBodyStream(), "docs");
                if (docs.TryGet("Ids", out BlittableJsonReaderArray array) == false)
                    ThrowRequiredPropertyNameInRequest("Ids");

                var ids = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    ids[i] = array.GetStringByIndex(i);
                }

                context.OpenReadTransaction();
                GetDocumentsById(context, new StringValues(ids), metadataOnly);
            }
        }

        private void GetDocuments(DocumentsOperationContext context, bool metadataOnly)
        {
            var sw = Stopwatch.StartNew();

            // everything here operates on all docs
            var databaseChangeVector = DocumentsStorage.GetDatabaseChangeVector(context);

            if (GetStringFromHeaders("If-None-Match") == databaseChangeVector)
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }
            HttpContext.Response.Headers["ETag"] = "\"" + databaseChangeVector + "\"";

            var etag = GetLongQueryString("etag", false);
            var start = GetStart();
            var pageSize = GetPageSize();
            var isStartsWith = HttpContext.Request.Query.ContainsKey("startsWith");

            IEnumerable<Document> documents;
            if (etag != null)
            {
                documents = Database.DocumentsStorage.GetDocumentsFrom(context, etag.Value, start, pageSize);
            }
            else if (isStartsWith)
            {
                documents = Database.DocumentsStorage.GetDocumentsStartingWith(context,
                     HttpContext.Request.Query["startsWith"],
                     HttpContext.Request.Query["matches"],
                     HttpContext.Request.Query["exclude"],
                     HttpContext.Request.Query["startAfter"],
                     start,
                     pageSize);
            }
            else // recent docs
            {
                documents = Database.DocumentsStorage.GetDocumentsInReverseEtagOrder(context, start, pageSize);
            }

            int numberOfResults;

            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Results");

                writer.WriteDocuments(context, documents, metadataOnly, out numberOfResults);

                writer.WriteEndObject();
            }

            AddPagingPerformanceHint(PagingOperationType.Documents, isStartsWith ? nameof(DocumentsStorage.GetDocumentsStartingWith) : nameof(GetDocuments), HttpContext, numberOfResults, pageSize, sw.Elapsed);
        }

        private void GetDocumentsById(DocumentsOperationContext context, StringValues ids, bool metadataOnly)
        {
            var sw = Stopwatch.StartNew();

            var includePaths = GetStringValuesQueryString("include", required: false);
            var documents = new List<Document>(ids.Count);
            var includes = new List<Document>(includePaths.Count * ids.Count);
            var includeDocs = new IncludeDocumentsCommand(Database.DocumentsStorage, context, includePaths);
            foreach (var id in ids)
            {
                var document = Database.DocumentsStorage.Get(context, id);
                if (ids.Count == 1 && document == null)
                {
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                documents.Add(document);
                includeDocs.Gather(document);
            }

            includeDocs.Fill(includes);

            var actualEtag = ComputeEtagFor(documents, includes);

            var etag = GetStringFromHeaders("If-None-Match");
            if (etag == actualEtag)
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }

            HttpContext.Response.Headers[Constants.Headers.Etag] = "\"" + actualEtag + "\"";

            int numberOfResults;
            var blittable = GetBoolValueQueryString("blittable", required: false) ?? false;
            if (blittable)
            {
                WriteDocumentsBlittable(context, documents, includes, out numberOfResults);
            }
            else
            {
                WriteDocumentsJson(context, metadataOnly, documents, includes, out numberOfResults);
            }

            AddPagingPerformanceHint(PagingOperationType.Documents, nameof(GetDocumentsById), HttpContext, numberOfResults, documents.Count, sw.Elapsed);
        }

        private void WriteDocumentsJson(JsonOperationContext context, bool metadataOnly, IEnumerable<Document> documentsToWrite, List<Document> includes, out int numberOfResults)
        {
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(GetDocumentResult.Results));
                writer.WriteDocuments(context, documentsToWrite, metadataOnly, out numberOfResults);

                writer.WriteComma();
                writer.WritePropertyName(nameof(GetDocumentResult.Includes));
                if (includes.Count > 0)
                {
                    writer.WriteIncludes(context, includes);
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }
        }

        private void WriteDocumentsBlittable(DocumentsOperationContext context, IEnumerable<Document> documentsToWrite, List<Document> includes, out int numberOfResults)
        {
            numberOfResults = 0;
            HttpContext.Response.Headers["Content-Type"] = "binary/blittable-json";

            using (var streamBuffer = new UnmanagedStreamBuffer(context, ResponseBodyStream()))
            using (var writer = new ManualBlittableJsonDocumentBuilder<UnmanagedStreamBuffer>(context,
                null, new BlittableWriter<UnmanagedStreamBuffer>(context, streamBuffer)))
            {
                writer.StartWriteObjectDocument();

                writer.StartWriteObject();
                writer.WritePropertyName(nameof(GetDocumentResult.Results));

                writer.StartWriteArray();

                foreach (var document in documentsToWrite)
                {
                    numberOfResults++;
                    writer.WriteEmbeddedBlittableDocument(document.Data);
                }

                writer.WriteArrayEnd();

                writer.WritePropertyName(nameof(GetDocumentResult.Includes));

                writer.StartWriteObject();

                foreach (var include in includes)
                {
                    writer.WritePropertyName(include.Id);
                    writer.WriteEmbeddedBlittableDocument(include.Data);
                }

                writer.WriteObjectEnd();

                writer.WriteObjectEnd();

                writer.FinalizeDocument();
            }
        }

        private static unsafe string ComputeEtagFor(List<Document> documents, List<Document> includes)
        {
            // This method is efficient because we aren't materializing any values
            // except the etag, which we need
            if (documents.Count == 1 && (includes == null || includes.Count == 0))
                return documents[0]?.ChangeVector ?? string.Empty;

            var size = Sodium.crypto_generichash_bytes();
            Debug.Assert((int)size == 32);
            var cryptoGenerichashStatebytes = Sodium.crypto_generichash_statebytes();
            byte* state = stackalloc byte[cryptoGenerichashStatebytes];
            if (Sodium.crypto_generichash_init(state, null, UIntPtr.Zero, size) != 0)
                ThrowFailToInitHash();

            foreach (var doc in documents)
            {
                HashDocumentByChangeVector(state, doc);
            }

            if (includes != null)
            {
                foreach (var doc in includes)
                {
                    if (doc is IncludeDocumentsCommand.ConflictDocument)
                        continue;

                    HashDocumentByChangeVector(state, doc);
                }
            }

            byte* final = stackalloc byte[(int)size];
            if (Sodium.crypto_generichash_final(state, final, size) != 0)
                ThrowFailedToFinalizeHash();

            var str = new string(' ', 49);
            fixed (char* p = str)
            {
                p[0] = 'H';
                p[1] = 'a';
                p[2] = 's';
                p[3] = 'h';
                p[4] = '-';
                var len = Base64.ConvertToBase64Array(p + 5, final, 0, 32);
                Debug.Assert(len == 44);
            }

            return str;
        }

        private static unsafe void HashDocumentByChangeVector(byte* state, Document document)
        {
            if (document == null)
            {
                if (Sodium.crypto_generichash_update(state, null, 0) != 0)
                    ThrowFailedToUpdateHash();
            }
            else
                HashChangeVector(state, document.ChangeVector);
        }

        private static unsafe void HashChangeVector(byte* state, string changeVector)
        {
            fixed (char* pCV = changeVector)
            {
                if (Sodium.crypto_generichash_update(state, (byte*)pCV, (ulong)(sizeof(char) * changeVector.Length)) != 0)
                    ThrowFailedToUpdateHash();
            }
        }

        private static void ThrowFailedToFinalizeHash()
        {
            throw new InvalidOperationException("Failed to finalize generic hash");
        }

        private static void ThrowFailToInitHash()
        {
            throw new InvalidOperationException("Failed to initiate generic hash");
        }

        private static void ThrowFailedToUpdateHash()
        {
            throw new InvalidOperationException("Failed to udpate generic hash");
        }

        [RavenAction("/databases/*/docs", "DELETE", AuthorizationStatus.ValidUser)]
        public async Task Delete()
        {
            var id = GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");
            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            {
                var changeVector = context.GetLazyString(GetStringFromHeaders("If-Match"));

                var cmd = new DeleteDocumentCommand(id, changeVector, Database, catchConcurrencyErrors: true);
                await Database.TxMerger.Enqueue(cmd);
                cmd.ExceptionDispatchInfo?.Throw();
            }

            NoContentStatus();
        }

        [RavenAction("/databases/*/docs", "PUT", AuthorizationStatus.ValidUser)]
        public async Task Put()
        {
            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            {
                var id = GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");

                var doc = context.ReadForDiskAsync(RequestBodyStream(), id).ConfigureAwait(false);

                if (id[id.Length - 1] == '|')
                {
                    var (_, clusterId) = await ServerStore.GenerateClusterIdentityAsync(id, Database.Name);
                    id = clusterId;
                }

                var changeVector = context.GetLazyString(GetStringQueryString("If-Match", false));

                var cmd = new MergedPutCommand(await doc, id, changeVector, Database);

                await Database.TxMerger.Enqueue(cmd);

                cmd.ExceptionDispatchInfo?.Throw();

                HttpContext.Response.StatusCode = (int)HttpStatusCode.Created;

                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName(nameof(PutResult.Id));
                    writer.WriteString(cmd.PutResult.Id);
                    writer.WriteComma();

                    writer.WritePropertyName(nameof(PutResult.ChangeVector));
                    writer.WriteString(cmd.PutResult.ChangeVector);

                    writer.WriteEndObject();
                }
            }
        }

        [RavenAction("/databases/*/docs", "PATCH", AuthorizationStatus.ValidUser)]
        public async Task Patch()
        {
            var id = GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");

            var isTest = GetBoolValueQueryString("test", required: false) ?? false;
            var debugMode = GetBoolValueQueryString("debug", required: false) ?? isTest;
            var skipPatchIfChangeVectorMismatch = GetBoolValueQueryString("skipPatchIfChangeVectorMismatch", required: false) ?? false;

            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            {
                var request = context.Read(RequestBodyStream(), "ScriptedPatchRequest");
                if (request.TryGet("Patch", out BlittableJsonReaderObject patchCmd) == false || patchCmd == null)
                    throw new ArgumentException("The 'Patch' field in the body request is mandatory");

                var patch = PatchRequest.Parse(patchCmd, out var patchArgs);

                PatchRequest patchIfMissing = null;
                BlittableJsonReaderObject patchIfMissingArgs = null;
                if (request.TryGet("PatchIfMissing", out BlittableJsonReaderObject patchIfMissingCmd) && patchIfMissingCmd != null)
                    patchIfMissing = PatchRequest.Parse(patchIfMissingCmd, out patchIfMissingArgs);

                var changeVector = context.GetLazyString(GetStringFromHeaders("If-Match"));

                using (var command = new PatchDocumentCommand(context,
                    id,
                    changeVector,
                    skipPatchIfChangeVectorMismatch,
                    (patch, patchArgs),
                    (patchIfMissing, patchIfMissingArgs),
                    Database,
                    isTest,
                    debugMode
                ))
                {
                    if (isTest == false)
                    {
                        await Database.TxMerger.Enqueue(command);
                    }
                    else
                    {
                        // PutDocument requires the write access to the docs storage
                        // testing patching is rare enough not to optimize it
                        using (context.OpenWriteTransaction())
                        {
                            command.Execute(context);
                        }
                    }

                    switch (command.PatchResult.Status)
                    {
                        case PatchStatus.DocumentDoesNotExist:
                            HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            return;
                        case PatchStatus.Created:
                            HttpContext.Response.StatusCode = (int)HttpStatusCode.Created;
                            break;
                        case PatchStatus.Skipped:
                            HttpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                            return;
                        case PatchStatus.Patched:
                        case PatchStatus.NotModified:
                            HttpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                    {
                        writer.WriteStartObject();

                        writer.WritePropertyName(nameof(command.PatchResult.Status));
                        writer.WriteString(command.PatchResult.Status.ToString());
                        writer.WriteComma();

                        writer.WritePropertyName(nameof(command.PatchResult.ModifiedDocument));
                        writer.WriteObject(command.PatchResult.ModifiedDocument);

                        if (debugMode)
                        {
                            writer.WriteComma();
                            writer.WritePropertyName(nameof(command.PatchResult.OriginalDocument));
                            if (isTest)
                                writer.WriteObject(command.PatchResult.OriginalDocument);
                            else
                                writer.WriteNull();

                            writer.WriteComma();

                            writer.WritePropertyName(nameof(command.PatchResult.Debug));

                            context.Write(writer, new DynamicJsonValue
                            {
                                ["Info"] = new DynamicJsonArray(command.DebugOutput),
                                ["Actions"] = command.DebugActions?.GetDebugActions()
                            });
                        }


                        writer.WriteEndObject();
                    }
                }
            }
        }

        [RavenAction("/databases/*/docs/class", "GET", AuthorizationStatus.ValidUser)]
        public Task GenerateClassFromDocument()
        {
            var id = GetStringQueryString("id");
            var lang = (GetStringQueryString("lang", required: false) ?? "csharp")
                .Trim().ToLowerInvariant();

            using (ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
            using (context.OpenReadTransaction())
            {
                var document = Database.DocumentsStorage.Get(context, id);
                if (document == null)
                {
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return Task.CompletedTask;
                }

                switch (lang)
                {
                    case "csharp":
                        break;
                    default:
                        throw new NotImplementedException($"Document code generator isn't implemeted for {lang}");
                }

                using (var writer = new StreamWriter(ResponseBodyStream()))
                {
                    var codeGenerator = new JsonClassGenerator(lang);
                    var code = codeGenerator.Execute(document);
                    writer.Write(code);
                }

                return Task.CompletedTask;
            }
        }

        private class MergedPutCommand : TransactionOperationsMerger.MergedTransactionCommand
        {
            private readonly string _id;
            private readonly LazyStringValue _expectedChangeVector;
            private readonly BlittableJsonReaderObject _document;
            private readonly DocumentDatabase _database;

            public ExceptionDispatchInfo ExceptionDispatchInfo;
            public DocumentsStorage.PutOperationResults PutResult;

            public MergedPutCommand(BlittableJsonReaderObject doc, string id, LazyStringValue changeVector, DocumentDatabase database)
            {
                _document = doc;
                _id = id;
                _expectedChangeVector = changeVector;
                _database = database;
            }

            public override int Execute(DocumentsOperationContext context)
            {
                try
                {
                    PutResult = _database.DocumentsStorage.Put(context, _id, _expectedChangeVector, _document);
                }
                catch (ConcurrencyException e)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(e);
                }
                return 1;
            }
        }
    }
}
