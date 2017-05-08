// -----------------------------------------------------------------------
//  <copyright file="SmugglerHandler.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jint;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Smuggler;
using Raven.Client.Util;
using Raven.Server.Documents;
using Raven.Server.Documents.Operations;
using Raven.Server.Json;
using Raven.Server.Routing;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Raven.Server.Smuggler.Documents.Data;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Smuggler.Documents.Handlers
{
    public class SmugglerHandler : DatabaseRequestHandler
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        [RavenAction("/databases/*/smuggler/validate-options", "POST")]
        public async Task PostValidateOptions()
        {
            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            using (context.OpenReadTransaction())
            {
                var blittableJson = await context.ReadForMemoryAsync(RequestBodyStream(), "");
                var options = JsonDeserializationServer.DatabaseSmugglerOptions(blittableJson);

                if (!string.IsNullOrEmpty(options.FileName) && options.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException($"{options.FileName} is Invalid File Name");

                if (string.IsNullOrEmpty(options?.TransformScript))
                {
                    NoContentStatus();
                    return;
                }

                try
                {
                    var jint = new Engine(cfg =>
                    {
                        cfg.AllowDebuggerStatement(false);
                        cfg.MaxStatements(options.MaxStepsForTransformScript);
                        cfg.NullPropagation();
                    });

                    jint.Execute(string.Format(@"
                    function Transform(docInner){{
                        return ({0}).apply(this, [docInner]);
                    }};", options.TransformScript));
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Incorrect transform script", e);
                }

                NoContentStatus();
            }
        }

        [RavenAction("/databases/*/smuggler/export", "POST")]
        public async Task PostExport()
        {
            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                var operationId = GetLongQueryString("operationId", required: false);

                var stream = TryGetRequestFormStream("DownloadOptions") ?? RequestBodyStream();

                var blittableJson = await context.ReadForMemoryAsync(stream, "DownloadOptions");
                var options = JsonDeserializationServer.DatabaseSmugglerOptions(blittableJson);

                var token = CreateOperationToken();

                var fileName = options.FileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"Dump of {context.DocumentDatabase.Name} {SystemTime.UtcNow.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture)}";
                }

                var contentDisposition = "attachment; filename=" + Uri.EscapeDataString(fileName) + ".ravendbdump";
                HttpContext.Response.Headers["Content-Disposition"] = contentDisposition;

                if (operationId.HasValue)
                {
                    try
                    {
                        await
                            Database.Operations.AddOperation(
                                "Export database: " + Database.Name,
                                DatabaseOperations.OperationType.DatabaseExport,
                                onProgress => Task.Run(() => ExportDatabaseInternal(options, onProgress, context, token), token.Token), operationId.Value, token);
                    }
                    catch (Exception)
                    {
                        HttpContext.Abort();
                    }

                }
                else
                {
                    ExportDatabaseInternal(options, null, context, token);
                }
            }
        }

        private IOperationResult ExportDatabaseInternal(DatabaseSmugglerOptions options, Action<IOperationProgress> onProgress, DocumentsOperationContext context, OperationCancelToken token)
        {
            using (token)
            {
                var source = new DatabaseSource(Database, 0, 0);
                var destination = new StreamDestination(ResponseBodyStream(), context);
                var smuggler = new DatabaseSmuggler(source, destination, Database.Time, options, onProgress: onProgress, token: token.Token);
                return smuggler.Execute();
            }
        }

        [RavenAction("/databases/*/smuggler/import-s3-dir", "GET")]
        public async Task PostImportFromS3Directory()
        {
            var url = GetQueryStringValueAndAssertIfSingleAndNotEmpty("url");
            var result = await HttpClient.GetAsync(url);
            var dirTextXml = await result.Content.ReadAsStringAsync();
            var filesListing = XElement.Parse(dirTextXml);
            var ns = XNamespace.Get("http://s3.amazonaws.com/doc/2006-03-01/");
            var urls = from content in filesListing.Elements(ns + "Contents")
                let requestUri = url.TrimEnd('/') + "/" + content.Element(ns + "Key").Value
                select (Func<Task<Stream>>) (async () =>
                {
                    var respone = await HttpClient.GetAsync(requestUri);
                    if (respone.IsSuccessStatusCode == false)
                        throw new InvalidOperationException("Request failed on " + requestUri + " with " +
                                                            await respone.Content.ReadAsStreamAsync());
                    return await respone.Content.ReadAsStreamAsync();
                });

            var files = new BlockingCollection<Func<Task<Stream>>>(new ConcurrentQueue<Func<Task<Stream>>>(urls));
            files.CompleteAdding();
            await BulkImport(files, Path.GetTempPath());
        }

        [RavenAction("/databases/*/smuggler/import-dir", "GET")]
        public async Task PostImportDirectory()
        {
            var directory = GetQueryStringValueAndAssertIfSingleAndNotEmpty("dir");
            var files = new BlockingCollection<Func<Task<Stream>>>(new ConcurrentQueue<Func<Task<Stream>>>(
                    Directory.GetFiles(directory, "*.dump")
                        .Select(x => (Func<Task<Stream>>)(() => Task.FromResult<Stream>(File.OpenRead(x)))))
            );
            files.CompleteAdding();
            await BulkImport(files, directory);
        }

        private async Task BulkImport(BlockingCollection<Func<Task<Stream>>> files, string directory)
        {
            var results = new ConcurrentQueue<SmugglerResult>();
            var tasks = new Task[Math.Max(1, Environment.ProcessorCount / 2)];

            var finalResult = new SmugglerResult();

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    while (files.IsCompleted == false)
                    {
                        Func<Task<Stream>> getFile;
                        DocumentsOperationContext context;
                        try
                        {
                            getFile = files.Take();
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        using (ContextPool.AllocateOperationContext(out context))
                        using (var file = await getFile())
                        using (var stream = new GZipStream(new BufferedStream(file, 128 * Voron.Global.Constants.Size.Kilobyte), CompressionMode.Decompress))
                        {
                            var source = new StreamSource(stream, context);
                            var destination = new DatabaseDestination(Database);

                            var smuggler = new DatabaseSmuggler(source, destination, Database.Time);

                            var result = smuggler.Execute();
                            results.Enqueue(result);
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
            
            SmugglerResult importResult;
            while (results.TryDequeue(out importResult))
            {
                finalResult.Documents.SkippedCount += importResult.Documents.SkippedCount;
                finalResult.Documents.ReadCount += importResult.Documents.ReadCount;
                finalResult.Documents.ErroredCount += importResult.Documents.ErroredCount;
                finalResult.Documents.LastEtag = Math.Max(finalResult.Documents.LastEtag, importResult.Documents.LastEtag);

                finalResult.RevisionDocuments.ReadCount += importResult.RevisionDocuments.ReadCount;
                finalResult.RevisionDocuments.ErroredCount += importResult.RevisionDocuments.ErroredCount;
                finalResult.RevisionDocuments.LastEtag = Math.Max(finalResult.RevisionDocuments.LastEtag, importResult.RevisionDocuments.LastEtag);

                finalResult.Identities.ReadCount += importResult.Identities.ReadCount;
                finalResult.Identities.ErroredCount += importResult.Identities.ErroredCount;

                finalResult.Transformers.ReadCount += importResult.Transformers.ReadCount;
                finalResult.Transformers.ErroredCount += importResult.Transformers.ErroredCount;

                finalResult.Indexes.ReadCount += importResult.Indexes.ReadCount;
                finalResult.Indexes.ErroredCount += importResult.Indexes.ErroredCount;

                foreach (var message in importResult.Messages)
                    finalResult.AddMessage(message);
            }

            DocumentsOperationContext finalContext;
            using (ContextPool.AllocateOperationContext(out finalContext))
            {
                var memoryStream = new MemoryStream();
                WriteImportResult(finalContext, finalResult, memoryStream);
                memoryStream.Position = 0;
                try
                {
                    using (var output = File.Create(Path.Combine(directory, "smuggler.results.txt")))
                    {
                        memoryStream.CopyTo(output);
                    }
                }
                catch (Exception)
                {
                    // ignore any failure here
                }
                memoryStream.Position = 0;
                memoryStream.CopyTo(ResponseBodyStream());
            }
        }

        [RavenAction("/databases/*/smuggler/import", "GET")]
        public Task GetImport()
        {
            if (HttpContext.Request.Query.ContainsKey("file") == false &&
                HttpContext.Request.Query.ContainsKey("url") == false)
            {
                throw new ArgumentException("'file' or 'url' are mandatory when using GET /smuggler/import");
            }
            return PostImport();
        }

        [RavenAction("/databases/*/smuggler/import", "POST")]
        public async Task PostImport()
        {
            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                var options = DatabaseSmugglerOptionsServerSide.Create(HttpContext, context);
                var token = CreateOperationToken();

                using (var stream = new GZipStream(new BufferedStream(await GetImportStream(), 128 * Voron.Global.Constants.Size.Kilobyte), CompressionMode.Decompress))
                using (token)
                {
                    var source = new StreamSource(stream, context);
                    var destination = new DatabaseDestination(Database);

                    var smuggler = new DatabaseSmuggler(source, destination, Database.Time, options, token: token.Token);

                    var result = smuggler.Execute();

                    WriteImportResult(context, result, ResponseBodyStream());
                }
            }
        }

        [RavenAction("/databases/*/smuggler/import/async", "POST")]
        public async Task PostImportAsync()
        {
            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            {
                if (HttpContext.Request.HasFormContentType == false)
                {
                    HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest; // Bad request
                    using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                    {
                        context.Write(writer, new DynamicJsonValue
                        {
                            ["Type"] = "Error",
                            ["Error"] = "This endpoint requires form content type"
                        });
                        return;
                    }
                }

                var operationId = GetLongQueryString("operationId") ?? -1;
                var token = CreateOperationToken();

                var result = new SmugglerResult();
                await Database.Operations.AddOperation("Import to: " + Database.Name,
                    DatabaseOperations.OperationType.DatabaseImport,
                    onProgress =>
                    {
                        return Task.Run(async () =>
                        {
                            try
                            {
                                var boundary = MultipartRequestHelper.GetBoundary(
                                    MediaTypeHeaderValue.Parse(HttpContext.Request.ContentType),
                                    MultipartRequestHelper.MultipartBoundaryLengthLimit);
                                var reader = new MultipartReader(boundary, HttpContext.Request.Body);
                                DatabaseSmugglerOptions options = null;

                                while (true)
                                {
                                    var section = await reader.ReadNextSectionAsync().ConfigureAwait(false);
                                    if (section == null)
                                        break;

                                    ContentDispositionHeaderValue contentDisposition;
                                    var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition,
                                        out contentDisposition);

                                    if (hasContentDispositionHeader == false)
                                        continue;

                                    if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition))
                                    {
                                        var key = HeaderUtilities.RemoveQuotes(contentDisposition.Name);
                                        if (key != "importOptions")
                                            continue;

                                        var blittableJson = await context.ReadForMemoryAsync(section.Body, "importOptions");
                                        options = JsonDeserializationServer.DatabaseSmugglerOptions(blittableJson);
                                        continue;
                                    }

                                    if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition) == false)
                                        continue;

                                    var stream = new GZipStream(section.Body, CompressionMode.Decompress);
                                    DoImportInternal(context, stream, options, result, onProgress, token);
                                }
                            }
                            catch (Exception e)
                            {
                                result.AddError($"Error occured during export. Exception: {e.Message}");
                                throw;
                            }

                            return (IOperationResult)result;
                        });
                    }, operationId, token).ConfigureAwait(false);

                WriteImportResult(context, result, ResponseBodyStream());
            }
        }

        private void DoImportInternal(DocumentsOperationContext context, Stream stream, DatabaseSmugglerOptions options, SmugglerResult result, Action<IOperationProgress> onProgress, OperationCancelToken token)
        {
            using (stream)
            using (token)
            {
                var source = new StreamSource(stream, context);
                var destination = new DatabaseDestination(Database);
                var smuggler = new DatabaseSmuggler(source, destination, Database.Time, options, result, onProgress, token.Token);

                smuggler.Execute();
            }
        }

        private async Task<Stream> GetImportStream()
        {
            var file = GetStringQueryString("file", required: false);
            if (string.IsNullOrEmpty(file) == false)
                return File.OpenRead(file);

            var url = GetStringQueryString("url", required: false);
            if (string.IsNullOrEmpty(url) == false)
            {

                var stream = await HttpClient.GetStreamAsync(url);
                return stream;
            }

            return HttpContext.Request.Body;
        }


        private static void WriteImportResult(JsonOperationContext context, SmugglerResult result, Stream stream)
        {
            using (var writer = new BlittableJsonTextWriter(context, stream))
            {
                var json = result.ToJson();
                context.Write(writer, json);
            }
        }
    }
}