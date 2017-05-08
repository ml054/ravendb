﻿// -----------------------------------------------------------------------
//  <copyright file="DocumentHandler.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Replication.Messages;
using Raven.Client.Extensions;
using Raven.Server.Extensions;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;
using Sparrow;
using Sparrow.Json;
using Voron.Exceptions;

namespace Raven.Server.Documents.Handlers
{
    public class AttachmentHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/attachments", "GET")]
        public Task Get()
        {
            return GetAttachment(true);
        }

        [RavenAction("/databases/*/attachments", "POST")]
        public Task GetPost()
        {
            return GetAttachment(false);
        }

        private async Task GetAttachment(bool isDocument)
        {
            var documentId = GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");
            var name = GetQueryStringValueAndAssertIfSingleAndNotEmpty("name");

            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            using (context.OpenReadTransaction())
            {
                AttachmentType type = AttachmentType.Document;
                ChangeVectorEntry[] changeVector = null;
                if (isDocument == false)
                {
                    var stream = TryGetRequestFormStream("ChangeVectorAndType") ?? RequestBodyStream();
                    var request = context.Read(stream, "GetAttachment");

                    string typeString;
                    if (request.TryGet("Type", out typeString) == false ||
                        Enum.TryParse(typeString, out type) == false)
                        throw new ArgumentException("The 'Type' field in the body request is mandatory");

                    BlittableJsonReaderArray changeVectorArray;
                    if (request.TryGet("ChangeVector", out changeVectorArray) == false)
                        throw new ArgumentException("The 'ChangeVector' field in the body request is mandatory");

                    changeVector = changeVectorArray.ToVector();
                }

                var attachment = Database.DocumentsStorage.AttachmentsStorage.GetAttachment(context, documentId, name, type, changeVector);
                if (attachment == null)
                {
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                var etag = GetLongFromHeaders("If-None-Match");
                if (etag == attachment.Etag)
                {
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                    return;
                }

                try
                {
                    var fileName = Path.GetFileName(attachment.Name);
                    fileName = Uri.EscapeDataString(fileName);
                    HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"; filename*=UTF-8''{fileName}";
                }
                catch (ArgumentException e)
                {
                    if (Logger.IsInfoEnabled)
                        Logger.Info($"Skip Content-Disposition header because of not valid file name: {attachment.Name}", e);
                }
                try
                {
                    HttpContext.Response.Headers["Content-Type"] = attachment.ContentType.ToString();
                }
                catch (InvalidOperationException e)
                {
                    if (Logger.IsInfoEnabled)
                        Logger.Info($"Skip Content-Type header because of not valid content type: {attachment.ContentType}", e);
                    if (HttpContext.Response.Headers.ContainsKey("Content-Type"))
                        HttpContext.Response.Headers.Remove("Content-Type");
                }
                HttpContext.Response.Headers["Content-Hash"] = attachment.Base64Hash.ToString();
                HttpContext.Response.Headers[Constants.Headers.Etag] = $"\"{attachment.Etag}\"";

                JsonOperationContext.ManagedPinnedBuffer buffer;
                using (context.GetManagedBuffer(out buffer))
                using (var stream = attachment.Stream)
                {
                    var responseStream = ResponseBodyStream();
                    var count = await stream.ReadAsync(buffer.Buffer.Array, 0, buffer.Length, Database.DatabaseShutdown);
                    while (count > 0)
                    {
                        await responseStream.WriteAsync(buffer.Buffer.Array, 0, count, Database.DatabaseShutdown);
                        count = await stream.ReadAsync(buffer.Buffer.Array, 0, buffer.Length, Database.DatabaseShutdown);
                    }
                }
            }
        }

        [RavenAction("/databases/*/attachments", "PUT")]
        public async Task Put()
        {
            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                var id = GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");
                var name = GetQueryStringValueAndAssertIfSingleAndNotEmpty("name");
                var contentType = GetStringQueryString("contentType", false) ?? "";

                AttachmentResult result;
                FileStream file;
                using (Database.DocumentsStorage.AttachmentsStorage.GetTempFile(out file))
                {
                    JsonOperationContext.ManagedPinnedBuffer buffer;
                    using (context.GetManagedBuffer(out buffer))
                    {
                        var requestStream = RequestBodyStream();
                        var metroCtx = Hashing.Streamed.Metro128.BeginProcess();
                        var xxhas64Ctx = Hashing.Streamed.XXHash64.BeginProcess();
                        var bufferRead = 0;
                        while (true)
                        {
                            var count = await requestStream.ReadAsync(buffer.Buffer.Array, buffer.Buffer.Offset + bufferRead, buffer.Buffer.Count - bufferRead, Database.DatabaseShutdown);
                            if (count == 0)
                                break;

                            bufferRead += count;

                            if (bufferRead == buffer.Buffer.Count)
                            {
                                PartialComputeHash(metroCtx,xxhas64Ctx, buffer, bufferRead);
                                await file.WriteAsync(buffer.Buffer.Array, buffer.Buffer.Offset, bufferRead, Database.DatabaseShutdown);
                                bufferRead = 0;
                            }
                        }
                        await file.WriteAsync(buffer.Buffer.Array, buffer.Buffer.Offset, bufferRead, Database.DatabaseShutdown);
                        file.Position = 0;
                        PartialComputeHash(metroCtx, xxhas64Ctx, buffer, bufferRead);
                        var hash = FinalizeGetHash(metroCtx, xxhas64Ctx);

                        var etag = GetLongFromHeaders("If-Match");

                        var cmd = new MergedPutAttachmentCommand
                        {
                            Database = Database,
                            ExpectedEtag = etag,
                            DocumentId = id,
                            Name = name,
                            Stream = file,
                            Hash = hash,
                            ContentType = contentType
                        };
                        await Database.TxMerger.Enqueue(cmd);
                        cmd.ExceptionDispatchInfo?.Throw();
                        result = cmd.Result;
                    }
                }

                HttpContext.Response.StatusCode = (int)HttpStatusCode.Created;

                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName(nameof(AttachmentResult.Etag));
                    writer.WriteInteger(result.Etag);
                    writer.WriteComma();

                    writer.WritePropertyName(nameof(AttachmentResult.Name));
                    writer.WriteString(result.Name);
                    writer.WriteComma();

                    writer.WritePropertyName(nameof(AttachmentResult.DocumentId));
                    writer.WriteString(result.DocumentId);
                    writer.WriteComma();

                    writer.WritePropertyName(nameof(AttachmentResult.ContentType));
                    writer.WriteString(result.ContentType);
                    writer.WriteComma();

                    writer.WritePropertyName(nameof(AttachmentResult.Hash));
                    writer.WriteString(result.Hash);
                    writer.WriteComma();

                    writer.WritePropertyName(nameof(AttachmentResult.Size));
                    writer.WriteInteger(result.Size);

                    writer.WriteEndObject();
                }
            }
        }

        private static unsafe string FinalizeGetHash(Hashing.Streamed.Metro128Context metroCtx, Hashing.Streamed.XXHash64Context xxhas64Ctx)
        {
            var metro128Hash = Hashing.Streamed.Metro128.EndProcess(metroCtx);
            var xxHash64 = Hashing.Streamed.XXHash64.EndProcess(xxhas64Ctx);

            var hash = new byte[sizeof(ulong) * 3];
            fixed (byte* pHash = hash)
            {
                var longs = (ulong*)pHash;
                longs[0] = metro128Hash.H1;
                longs[1] = metro128Hash.H2;
                longs[2] = xxHash64;
            }
            return Convert.ToBase64String(hash);
        }

        private static unsafe void PartialComputeHash(Hashing.Streamed.Metro128Context metroCtx, Hashing.Streamed.XXHash64Context xxHash64Context, JsonOperationContext.ManagedPinnedBuffer buffer, int bufferRead)
        {
            Hashing.Streamed.Metro128.Process(metroCtx, buffer.Pointer, bufferRead);
            Hashing.Streamed.XXHash64.Process(xxHash64Context, buffer.Pointer, bufferRead);
        }

        [RavenAction("/databases/*/attachments", "DELETE")]
        public async Task Delete()
        {
            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                var id = GetQueryStringValueAndAssertIfSingleAndNotEmpty("id");
                var name = GetQueryStringValueAndAssertIfSingleAndNotEmpty("name");

                var etag = GetLongFromHeaders("If-Match");

                var cmd = new MergedDeleteAttachmentCommand
                {
                    Database = Database,
                    ExpectedEtag = etag,
                    DocumentId = id,
                    Name = name,
                };
                await Database.TxMerger.Enqueue(cmd);
                cmd.ExceptionDispatchInfo?.Throw();

                NoContentStatus();
            }
        }

        private class MergedPutAttachmentCommand : TransactionOperationsMerger.MergedTransactionCommand
        {
            public string DocumentId;
            public string Name;
            public long? ExpectedEtag;
            public DocumentDatabase Database;
            public ExceptionDispatchInfo ExceptionDispatchInfo;
            public AttachmentResult Result;
            public string ContentType;
            public Stream Stream;
            public string Hash;

            public override int Execute(DocumentsOperationContext context)
            {
                try
                {
                    Result = Database.DocumentsStorage.AttachmentsStorage.PutAttachment(context, DocumentId, Name, ContentType, Hash, ExpectedEtag, Stream);
                }
                catch (ConcurrencyException e)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(e);
                }
                return 1;
            }
        }

        private class MergedDeleteAttachmentCommand : TransactionOperationsMerger.MergedTransactionCommand
        {
            public string DocumentId;
            public string Name;
            public long? ExpectedEtag;
            public DocumentDatabase Database;
            public ExceptionDispatchInfo ExceptionDispatchInfo;

            public override int Execute(DocumentsOperationContext context)
            {
                try
                {
                    Database.DocumentsStorage.AttachmentsStorage.DeleteAttachment(context, DocumentId, Name, ExpectedEtag);
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