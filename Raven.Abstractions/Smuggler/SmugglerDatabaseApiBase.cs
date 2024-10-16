#if !DNXCORE50
using Raven.Abstractions.Connection;
using Raven.Abstractions.Data;
using Raven.Abstractions.Exceptions;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Json;
using Raven.Abstractions.Logging;
using Raven.Abstractions.Smuggler.Data;
using Raven.Abstractions.Util;
using Raven.Imports.Newtonsoft.Json;
using Raven.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Raven.Database.Data;

namespace Raven.Abstractions.Smuggler
{
    public abstract class SmugglerDatabaseApiBase : ISmugglerApi<RavenConnectionStringOptions, SmugglerDatabaseOptions, OperationState>
    {
        const int RetriesCount = 5;

        public ISmugglerDatabaseOperations Operations { get; protected set; }

        public SmugglerDatabaseOptions Options { get; private set; }

        private const string IncrementalExportStateFile = "IncrementalExport.state.json";

        protected SmugglerDatabaseApiBase(SmugglerDatabaseOptions options)
        {
            Options = options;
        }

        protected bool IgnoreErrorsAndContinue
        {
            get
            {
                return Options != null && Options.IgnoreErrorsAndContinue;
            }
        }

        public virtual async Task<OperationState> ExportData(SmugglerExportOptions<RavenConnectionStringOptions> exportOptions)
        {
            Operations.Configure(Options);
            Operations.Initialize(Options);

            var result = new OperationState
            {
                FilePath = exportOptions.ToFile,
                LastAttachmentsEtag = Options.StartAttachmentsEtag,
                LastDocsEtag = Options.StartDocsEtag,
                LastDocDeleteEtag = Options.StartDocsDeletionEtag,
                LastAttachmentsDeleteEtag = Options.StartAttachmentsDeletionEtag
            };

            if (Options.Incremental)
            {
                if (Directory.Exists(result.FilePath) == false)
                {
                    if (File.Exists(result.FilePath))
                        result.FilePath = Path.GetDirectoryName(result.FilePath) ?? result.FilePath;
                    else
                        Directory.CreateDirectory(result.FilePath);
                }

                if (Options.StartDocsEtag == Etag.Empty && Options.StartAttachmentsEtag == Etag.Empty)
                {
                    ReadLastEtagsFromFile(result);
                }

                result.FilePath = Path.Combine(result.FilePath, SystemTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-0", CultureInfo.InvariantCulture) + ".ravendb-incremental-dump");
                if (File.Exists(result.FilePath))
                {
                    var counter = 1;
                    while (true)
                    {
                        result.FilePath = Path.Combine(Path.GetDirectoryName(result.FilePath), SystemTime.UtcNow.ToString("yyyy-MM-dd-HH-mm", CultureInfo.InvariantCulture) + "-" + counter + ".ravendb-incremental-dump");

                        if (File.Exists(result.FilePath) == false)
                            break;
                        counter++;
                    }
                }
            }

            SmugglerExportException lastException = null;

            bool ownedStream = exportOptions.ToStream == null;
            var stream = exportOptions.ToStream ?? File.Create(result.FilePath);

            try
            {
                await DetectServerSupportedFeatures(exportOptions.From).ConfigureAwait(false);
            }
            catch (WebException e)
            {
                Operations.ShowProgress("Failed to query server for supported features. Reason : " + e.Message);
                SetLegacyMode();
            }

            try
            {
                using (var gZipStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
                using (var streamWriter = new StreamWriter(gZipStream))
                {
                    var jsonWriter = new JsonTextWriter(streamWriter)
                    {
                        Formatting = Formatting.Indented
                    };

                    var isLastExport = false;
                    var watch = Stopwatch.StartNew();

                    jsonWriter.WriteStartObject();
                    while (true)
                    {
                        if (isLastExport == false)
                        {
                            var maxEtags = Operations.FetchCurrentMaxEtags();
                            lastException = await RunSingleExportAsync(exportOptions, result, maxEtags, jsonWriter, ownedStream).ConfigureAwait(false);
                        }
                        else
                            await RunLastExportAsync(exportOptions, result, jsonWriter).ConfigureAwait(false);

                        if (lastException != null)
                            break;

                        if (isLastExport)
                            break;

                        var elapsedMinutes = watch.Elapsed.TotalMinutes;
                        if (elapsedMinutes < 30)
                            isLastExport = true;

                        watch.Restart();
                    }

                    jsonWriter.WriteEndObject();
                    streamWriter.Flush();
                }

                if (Options.Incremental)
                    WriteLastEtagsToFile(result, result.FilePath, IncrementalExportStateFile);

                if (Options.ExportDeletions)
                    Operations.PurgeTombstones(result);

                if (lastException != null)
                    throw lastException;

                return result;
            }
            finally
            {
                if (ownedStream && stream != null)
                    stream.Dispose();
            }
        }

        private async Task RunLastExportAsync(SmugglerExportOptions<RavenConnectionStringOptions> exportOptions, OperationState state, JsonTextWriter writer)
        {
            var now = SystemTime.UtcNow;

            Debug.Assert(exportOptions != null);
            Debug.Assert(state != null);
            Debug.Assert(writer != null);

            writer.WritePropertyName("Indexes");
            writer.WriteStartArray();
            if (Options.OperateOnTypes.HasFlag(ItemType.Indexes))
                await ExportIndexes(exportOptions.From, writer).ConfigureAwait(false);

            writer.WriteEndArray();

            writer.WritePropertyName("Transformers");
            writer.WriteStartArray();
            if (Options.OperateOnTypes.HasFlag(ItemType.Transformers))
                await ExportTransformers(exportOptions.From, writer).ConfigureAwait(false);

            writer.WriteEndArray();

            using (var enumerator = await Operations.ExportItems(Options.OperateOnTypes, state).ConfigureAwait(false))
            {
                string currentProperty = null;
                LastEtagsInfo summary = null;
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    var current = enumerator.Current;
                    var type = (SmugglerExportType)Enum.Parse(typeof(SmugglerExportType), current.Value<string>("Type"), true);
                    var item = current.Value<RavenJObject>("Item");
                    if (type == SmugglerExportType.Summary)
                    {
                        summary = item.JsonDeserialization<LastEtagsInfo>();
                        break;
                    }

                    var property = GetPropertyName(type);

                    if (property != currentProperty)
                    {
                        if (currentProperty != null)
                            writer.WriteEndArray();

                        writer.WritePropertyName(property);
                        writer.WriteStartArray();
                        currentProperty = property;
                    }

                    if (FilterItem(type, item, now) == false)
                        continue;

                    item = await ModifyItemAsync(type, item).ConfigureAwait(false);
                    if (item == null)
                        continue;

                    item.WriteTo(writer);
                }

                if (currentProperty != null)
                    writer.WriteEndArray();

                if (summary != null)
                {
                    state.LastAttachmentsDeleteEtag = summary.LastAttachmentsDeleteEtag;
                    state.LastAttachmentsEtag = summary.LastAttachmentsEtag;
                    state.LastDocDeleteEtag = summary.LastDocDeleteEtag;
                    state.LastDocsEtag = summary.LastDocsEtag;
                }
            }
        }

        private async Task<RavenJObject> ModifyItemAsync(SmugglerExportType type, RavenJObject item)
        {
            if (type == SmugglerExportType.Document)
            {
                if (string.IsNullOrEmpty(Options.TransformScript) == false)
                    return await Operations.TransformDocument(item, Options.TransformScript).ConfigureAwait(false);
            }

            return item;
        }

        private bool FilterItem(SmugglerExportType type, RavenJToken item, DateTime now)
        {
            if (type == SmugglerExportType.Identity)
            {
                var identityName = item.Value<string>("Key");

                return FilterIdentity(identityName, Options.OperateOnTypes);
            }

            if (type == SmugglerExportType.Document)
            {
                return FilterDocument(item, now);
            }

            return true;
        }

        private bool FilterDocument(RavenJToken document, DateTime now)
        {
            if (Options.MatchFilters(document) == false)
            {
                return false;
            }

            if (Options.ShouldExcludeExpired && Options.ExcludeExpired(document, now))
            {
                return false;
            }

            return true;
        }

        private static string GetPropertyName(SmugglerExportType type)
        {
            var fieldInfo = type.GetType().GetField(type.ToString());
            var attribute = fieldInfo.GetCustomAttribute<DescriptionAttribute>();

            return attribute.Description;
        }

        private async Task<SmugglerExportException> RunSingleExportAsync(SmugglerExportOptions<RavenConnectionStringOptions> exportOptions, OperationState state, LastEtagsInfo maxEtags, JsonTextWriter writer, bool ownedStream)
        {
            Debug.Assert(exportOptions != null);
            Debug.Assert(state != null);
            Debug.Assert(maxEtags != null);
            Debug.Assert(writer != null);

            SmugglerExportException exception = null;

            writer.WritePropertyName("Docs");
            writer.WriteStartArray();
            if (Options.OperateOnTypes.HasFlag(ItemType.Documents))
            {
                try
                {
                    var operationStatus = await ExportDocuments(exportOptions.From, writer, state.LastDocsEtag, maxEtags.LastDocsEtag, Options.Limit - state.NumberOfExportedDocuments).ConfigureAwait(false);
                    state.LastDocsEtag = operationStatus.LastEtag;
                    state.NumberOfExportedDocuments = operationStatus.NumberOfExportedItems;
                }
                catch (SmugglerExportException e)
                {
                    state.LastDocsEtag = e.LastEtag;
                    e.File = ownedStream ? state.FilePath : null;
                    exception = e;
                }
            }
            writer.WriteEndArray();

            writer.WritePropertyName("Attachments");
            writer.WriteStartArray();
            if (Options.OperateOnTypes.HasFlag(ItemType.Attachments) && exception == null)
            {
                try
                {
                    var operationStatus = await ExportAttachments(exportOptions.From, writer, state.LastAttachmentsEtag, maxEtags.LastAttachmentsEtag, Options.Limit - state.NumberOfExportedAttachments).ConfigureAwait(false);
                    state.LastAttachmentsEtag = operationStatus.LastEtag;
                    state.NumberOfExportedAttachments = operationStatus.NumberOfExportedItems;
                }
                catch (SmugglerExportException e)
                {
                    state.LastAttachmentsEtag = e.LastEtag;
                    e.File = ownedStream ? state.FilePath : null;
                    exception = e;
                }
            }
            writer.WriteEndArray();

            if (Options.ExportDeletions)
                await ExportDeletions(writer, state, maxEtags).ConfigureAwait(false);

            return exception;
        }

        public bool FilterIdentity(string identityName, ItemType operateOnTypes)
        {
            if ("Raven/Etag".Equals(identityName, StringComparison.OrdinalIgnoreCase))
                return false;

            if ("IndexId".Equals(identityName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (Constants.RavenSubscriptionsPrefix.Equals(identityName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (operateOnTypes.HasFlag(ItemType.Documents))
                return true;

            return false;
        }

        public static void ReadLastEtagsFromFile(OperationState result, string etagFileLocation)
        {
            var log = LogManager.GetCurrentClassLogger();

            if (!File.Exists(etagFileLocation))
                return;

            using (var streamReader = new StreamReader(new FileStream(etagFileLocation, FileMode.Open)))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                RavenJObject ravenJObject;
                try
                {
                    ravenJObject = RavenJObject.Load(jsonReader);
                }
                catch (Exception e)
                {
                    log.WarnException("Could not parse etag document from file : " + etagFileLocation + ", ignoring, will start from scratch", e);
                    return;
                }
                result.LastDocsEtag = Etag.Parse(ravenJObject.Value<string>("LastDocEtag"));
                result.LastAttachmentsEtag = Etag.Parse(ravenJObject.Value<string>("LastAttachmentEtag"));
                result.LastDocDeleteEtag = Etag.Parse(ravenJObject.Value<string>("LastDocDeleteEtag") ?? Etag.Empty.ToString());
                result.LastAttachmentsDeleteEtag = Etag.Parse(ravenJObject.Value<string>("LastAttachmentsDeleteEtag") ?? Etag.Empty.ToString());
            }
        }

        public static void ReadLastEtagsFromFile(OperationState result)
        {
            var etagFileLocation = Path.Combine(result.FilePath, IncrementalExportStateFile);
            ReadLastEtagsFromFile(result, etagFileLocation);
        }

        public static void WriteLastEtagsToFile(OperationState result, string etagFileLocation)
        {
            using (var streamWriter = new StreamWriter(File.Create(etagFileLocation)))
            {
                new RavenJObject
                    {
                        {"LastDocEtag", result.LastDocsEtag.ToString()},
                        {"LastAttachmentEtag", result.LastAttachmentsEtag.ToString()},
                        {"LastDocDeleteEtag", result.LastDocDeleteEtag.ToString()},
                        {"LastAttachmentsDeleteEtag", result.LastAttachmentsDeleteEtag.ToString()}
                    }.WriteTo(new JsonTextWriter(streamWriter));
                streamWriter.Flush();
            }
        }
        public static void WriteLastEtagsToFile(OperationState result, string backupPath, string filename)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            var etagFileLocation = Path.Combine(Path.GetDirectoryName(backupPath), filename);
            WriteLastEtagsToFile(result, etagFileLocation);
        }

        private async Task ExportTransformers(RavenConnectionStringOptions src, JsonTextWriter jsonWriter)
        {
            var totalCount = 0;
            var retries = RetriesCount;

            while (true)
            {
                RavenJArray transformers;

                try
                {
                    transformers = await Operations.GetTransformers(src, totalCount);
                }
                catch (Exception e)
                {
                    if (retries-- == 0 & IgnoreErrorsAndContinue)
                    {
                        Operations.ShowProgress("Failed getting transformers too much times, stopping the transformer export entirely. Message: {0}", e.Message);
                        return;
                    }

                    if (IgnoreErrorsAndContinue == false)
                        throw;

                    Operations.ShowProgress("Failed fetching transformer information from exporting store. {0} retries remaining. Message: {1}", retries, e.Message);
                    continue;
                }

                if (transformers.Length == 0)
                {
                    Operations.ShowProgress("Done with reading transformers, total: {0}", totalCount);
                    break;
                }

                totalCount += transformers.Length;
                Operations.ShowProgress("Reading batch of {0,3} transformers, read so far: {1,10:#,#;;0}", transformers.Length, totalCount);

                foreach (var transformer in transformers)
                {
                    try
                    {
                        transformer.WriteTo(jsonWriter);
                    }
                    catch (Exception e)
                    {
                        if (IgnoreErrorsAndContinue == false)
                            throw;

                        Operations.ShowProgress("PUT of a transformer {0} failed. Message: {1}", transformer.Value<string>("name"), e.Message);
                    }
                }
            }
        }

        public abstract Task ExportDeletions(JsonTextWriter jsonWriter, OperationState result, LastEtagsInfo maxEtagsToFetch);

        [Obsolete("Use RavenFS instead.")]
        protected virtual async Task<ExportOperationStatus> ExportAttachments(RavenConnectionStringOptions src, JsonTextWriter jsonWriter, Etag lastEtag, Etag maxEtag, int maxNumberOfAttachmentsToExport)
        {
            var status = new ExportOperationStatus
            {
                LastEtag = lastEtag
            };

            var retries = RetriesCount;
            var maxEtagReached = false;

            while (true)
            {
                try
                {
                    if (maxNumberOfAttachmentsToExport - status.NumberOfExportedItems <= 0 || maxEtagReached)
                    {
                        Operations.ShowProgress("Done with reading attachments, total: {0}", status.NumberOfExportedItems);
                        status.LastEtag = lastEtag;
                        return status;
                    }

                    var maxRecords = Math.Min(maxNumberOfAttachmentsToExport - status.NumberOfExportedItems, Options.BatchSize);
                    List<AttachmentInformation> attachments;

                    try
                    {
                        attachments = await Operations.GetAttachments(status.NumberOfExportedItems, lastEtag, maxRecords);
                    }
                    catch (Exception e)
                    {
                        if (retries-- == 0 && IgnoreErrorsAndContinue)
                        {
                            status.LastEtag = Etag.InvalidEtag;
                            return status;
                        }

                        if (IgnoreErrorsAndContinue == false)
                            throw;

                        Operations.ShowProgress("Failed to get attachments. {0} retries remaining. Message: {1}", retries, e.Message);
                        continue;
                    }

                    if (attachments.Count == 0)
                    {
                        DatabaseStatistics databaseStatistics;
                        try
                        {
                            databaseStatistics = await Operations.GetStats();
                        }
                        catch (Exception e)
                        {
                            if (retries-- == 0 && IgnoreErrorsAndContinue)
                            {
                                status.LastEtag = Etag.Empty;
                                return status;
                            }

                            if (IgnoreErrorsAndContinue == false)
                                throw;

                            Operations.ShowProgress("Failed to get database statistics. Message: {0}", e.Message);
                            continue;
                        }

                        if (lastEtag == null) lastEtag = Etag.Empty;
                        if (lastEtag.CompareTo(databaseStatistics.LastAttachmentEtag) < 0)
                        {
                            lastEtag = EtagUtil.Increment(lastEtag, maxRecords);
                            Operations.ShowProgress("Got no results but didn't get to the last attachment etag, trying from: {0}",
                                         lastEtag);
                            continue;
                        }
                        Operations.ShowProgress("Done with reading attachments, total: {0}", status.NumberOfExportedItems);
                        status.LastEtag = lastEtag;
                        return status;
                    }

                    status.NumberOfExportedItems += attachments.Count;
                    Operations.ShowProgress("Reading batch of {0,3} attachments, read so far: {1,10:#,#;;0}", attachments.Count, status.NumberOfExportedItems);
                    foreach (var attachmentInformation in attachments)
                    {
                        if (maxEtag != null && attachmentInformation.Etag.CompareTo(maxEtag) > 0)
                        {
                            maxEtagReached = true;
                            break;
                        }

                        Operations.ShowProgress("Downloading attachment: {0}", attachmentInformation.Key);

                        try
                        {
                            var attachmentData = await Operations.GetAttachmentData(attachmentInformation);
                            if (attachmentData == null)
                                continue;

                            new RavenJObject
                            {
                                { "Data", attachmentData },
                                { "Metadata", attachmentInformation.Metadata },
                                { "Key", attachmentInformation.Key },
                                { "Etag", new RavenJValue(attachmentInformation.Etag.ToString()) }
                            }.WriteTo(jsonWriter);

                            lastEtag = attachmentInformation.Etag;
                        }
                        catch (Exception e)
                        {
                            if (IgnoreErrorsAndContinue == false)
                                throw;

                            Operations.ShowProgress("EXPORT of an attachment {0} failed. Message: {1}", attachmentInformation.Key, e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Operations.ShowProgress("Got Exception during smuggler export. Exception: {0}. ", e.Message);
                    Operations.ShowProgress("Done with reading attachments, total: {0}", status.NumberOfExportedItems, lastEtag);
                    throw new SmugglerExportException(e.Message, e)
                    {
                        LastEtag = lastEtag,
                    };
                }
            }
        }

        protected async Task<ExportOperationStatus> ExportDocuments(RavenConnectionStringOptions src, JsonTextWriter jsonWriter, Etag lastEtag, Etag maxEtag, int maxNumberOfDocumentsToExport)
        {
            var status = new ExportOperationStatus
            {
                LastEtag = lastEtag
            };

            var now = SystemTime.UtcNow;
            var lastReport = SystemTime.UtcNow;
            var reportInterval = TimeSpan.FromSeconds(2);
            var reachedMaxEtag = false;
            Operations.ShowProgress("Exporting Documents");
            int numberOfSkippedDocs = 0;
            var lastForcedFlush = SystemTime.UtcNow;
            var maxFlushInterval = TimeSpan.FromSeconds(1);

            while (true)
            {
                bool hasDocs = false;
                try
                {
                    var maxRecords = maxNumberOfDocumentsToExport - status.NumberOfExportedItems;
                    if (maxRecords > 0 && reachedMaxEtag == false)
                    {
                        var amountToFetchFromServer = Math.Min(Options.BatchSize, maxRecords);
                        using (var documents = await Operations.GetDocuments(src, lastEtag, amountToFetchFromServer).ConfigureAwait(false))
                        {
                            while (await documents.MoveNextAsync().ConfigureAwait(false))
                            {
                                if (numberOfSkippedDocs > 0 && (SystemTime.UtcNow - lastForcedFlush) > maxFlushInterval)
                                {
                                    Operations.ShowProgress("Skipped {0:#,#} documents", numberOfSkippedDocs);
                                    jsonWriter.WriteWhitespace(" ");
                                    jsonWriter.Flush();
                                    lastForcedFlush = SystemTime.UtcNow;
                                    numberOfSkippedDocs = 0;
                                }

                                hasDocs = true;
                                var document = documents.Current;

                                var tempLastEtag = Etag.Parse(document.Value<RavenJObject>("@metadata").Value<string>("@etag"));

                                Debug.Assert(!string.IsNullOrWhiteSpace(document.Value<RavenJObject>("@metadata").Value<string>("@id")));

                                if (maxEtag != null && tempLastEtag.CompareTo(maxEtag) > 0)
                                {
                                    reachedMaxEtag = true;
                                    break;
                                }
                                lastEtag = tempLastEtag;

                                if (!Options.MatchFilters(document))
                                {
                                    numberOfSkippedDocs++;
                                    continue;
                                }

                                if (Options.ShouldExcludeExpired && Options.ExcludeExpired(document, now))
                                {
                                    numberOfSkippedDocs++;
                                    continue;
                                }

                                if (string.IsNullOrEmpty(Options.TransformScript) == false)
                                    document = await Operations.TransformDocument(document, Options.TransformScript).ConfigureAwait(false);

                                // If document is null after a transform we skip it. 
                                if (document == null)
                                {
                                    numberOfSkippedDocs++;
                                    continue;
                                }

                                try
                                {
                                    document.WriteTo(jsonWriter);
                                }
                                catch (Exception e)
                                {
                                    if (IgnoreErrorsAndContinue == false)
                                        throw;

                                    Operations.ShowProgress("EXPORT of a document {0} failed. Message: {1}", document, e.Message);
                                }

                                status.NumberOfExportedItems++;

                                if (status.NumberOfExportedItems % 1000 == 0 || SystemTime.UtcNow - lastReport > reportInterval)
                                {
                                    Operations.ShowProgress("Exported {0} documents", status.NumberOfExportedItems);
                                    lastReport = SystemTime.UtcNow;
                                }
                            }
                        }

                        if (hasDocs)
                            continue;

                        // The server can filter all the results. In this case, we need to try to go over with the next batch.
                        // Note that if the ETag' server restarts number is not the same, this won't guard against an infinite loop.
                        // (This code provides support for legacy RavenDB version: 1.0)
                        var databaseStatistics = await Operations.GetStats();
                        var lastEtagComparable = new ComparableByteArray(lastEtag);
                        if (lastEtagComparable.CompareTo(databaseStatistics.LastDocEtag) < 0)
                        {
                            lastEtag = EtagUtil.Increment(lastEtag, amountToFetchFromServer);
                            if (lastEtag.CompareTo(databaseStatistics.LastDocEtag) >= 0)
                            {
                                lastEtag = databaseStatistics.LastDocEtag;
                            }
                            Operations.ShowProgress("Got no results but didn't get to the last doc etag, trying from: {0}", lastEtag);
                            continue;
                        }
                    }

                    // Load HiLo documents for selected collections
                    Options.Filters.ForEach(filter =>
                    {
                        if (string.Equals(filter.Path, "@metadata.Raven-Entity-Name", StringComparison.OrdinalIgnoreCase))
                        {
                            filter.Values.ForEach(collectionName =>
                            {
                                JsonDocument doc = Operations.GetDocument("Raven/Hilo/" + collectionName);
                                if (doc != null)
                                {
                                    doc.Metadata["@id"] = doc.Key;
                                    doc.ToJson().WriteTo(jsonWriter);
                                    status.NumberOfExportedItems++;
                                }
                            });
                        }
                    });

                    Operations.ShowProgress("Done with reading documents, total: {0}, lastEtag: {1}", status.NumberOfExportedItems, lastEtag);
                    status.LastEtag = lastEtag;
                    return status;
                }
                catch (Exception e)
                {
                    Operations.ShowProgress("Got Exception during smuggler export. Exception: {0}. ", e.Message);
                    Operations.ShowProgress("Done with reading documents, total: {0}, lastEtag: {1}", status.NumberOfExportedItems, lastEtag);
                    throw new SmugglerExportException(e.Message, e)
                    {
                        LastEtag = lastEtag,
                    };
                }
            }
        }

        public async Task WaitForIndexingAsOfLastWrite()
        {
            var stopwatch = Stopwatch.StartNew();
            var justIndexingWait = Stopwatch.StartNew();

            var stats = await Operations.GetStats();

            int tries = 0;
            Etag cutOffEtag = stats.LastDocEtag;
            while (true)
            {
                if (stats.Indexes.All(x => x.LastIndexedEtag.CompareTo(cutOffEtag) >= 0))
                {
                    Operations.ShowProgress("\rWaited {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);
                    break;
                }

                if (tries++ % 10 == 0)
                    Operations.ShowProgress("\rWaiting {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);

                Thread.Sleep(1000);
                stats = await Operations.GetStats();
            }

            stopwatch.Stop();
            justIndexingWait.Stop();
        }


        public async Task WaitForIndexing()
        {
            var stopwatch = Stopwatch.StartNew();
            var justIndexingWait = Stopwatch.StartNew();

            int tries = 0;
            while (true)
            {
                var stats = await Operations.GetStats();
                if (stats.StaleIndexes.Length != 0)
                {
                    if (tries++ % 10 == 0)
                        Operations.ShowProgress("\rWaiting {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);

                    Thread.Sleep(1000);
                    continue;
                }

                Operations.ShowProgress("\rWaited {0} for indexing ({1} total).", justIndexingWait.Elapsed, stopwatch.Elapsed);
                break;
            }

            stopwatch.Stop();
            justIndexingWait.Stop();
        }

        public virtual async Task ImportData(SmugglerImportOptions<RavenConnectionStringOptions> importOptions)
        {
            if (Options.Incremental == false)
            {
                Stream stream = importOptions.FromStream;
                bool ownStream = false;
                try
                {
                    if (stream == null)
                    {
                        stream = File.OpenRead(importOptions.FromFile);
                        ownStream = true;
                    }
                    await ImportData(importOptions, stream);
                }
                finally
                {
                    if (stream != null && ownStream)
                        stream.Dispose();
                }
                return;
            }

            var files = Directory.GetFiles(Path.GetFullPath(importOptions.FromFile))
                .Where(file => ".ravendb-incremental-dump".Equals(Path.GetExtension(file), StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(File.GetLastWriteTimeUtc)
                .ToArray();

            if (files.Length == 0)
                return;

            var oldItemType = Options.OperateOnTypes;

            Options.OperateOnTypes = Options.OperateOnTypes & ~(ItemType.Indexes | ItemType.Transformers);

            for (var i = 0; i < files.Length - 1; i++)
            {
                using (var fileStream = File.OpenRead(Path.Combine(importOptions.FromFile, files[i])))
                {
                    Operations.ShowProgress("Starting to import file: {0}", files[i]);
                    await ImportData(importOptions, fileStream);
                }
            }

            Options.OperateOnTypes = oldItemType;

            using (var fileStream = File.OpenRead(Path.Combine(importOptions.FromFile, files.Last())))
            {
                Operations.ShowProgress("Starting to import file: {0}", files.Last());
                await ImportData(importOptions, fileStream);
            }
        }

        public abstract Task Between(SmugglerBetweenOptions<RavenConnectionStringOptions> betweenOptions);

        public async virtual Task ImportData(SmugglerImportOptions<RavenConnectionStringOptions> importOptions, Stream stream)
        {
            Operations.Configure(Options);
            Operations.Initialize(Options);
            await DetectServerSupportedFeatures(importOptions.To);

            Stream sizeStream;

            var sw = Stopwatch.StartNew();
            // Try to read the stream compressed, otherwise continue uncompressed.
            JsonTextReader jsonReader;
            try
            {
                stream.Position = 0;
                sizeStream = new CountingStream(new GZipStream(stream, CompressionMode.Decompress));
                var streamReader = new StreamReader(sizeStream);

                jsonReader = new RavenJsonTextReader(streamReader);

                if (jsonReader.Read() == false)
                    return;
            }
            catch (Exception e)
            {
                if (e is InvalidDataException == false)
                    throw;

                stream.Seek(0, SeekOrigin.Begin);

                sizeStream = new CountingStream(stream);

                var streamReader = new StreamReader(sizeStream);

                jsonReader = new JsonTextReader(streamReader);

                if (jsonReader.Read() == false)
                    return;
            }

            if (jsonReader.TokenType != JsonToken.StartObject)
                throw new InvalidDataException("Invalid JSON format.");

            var exportCounts = new Dictionary<string, int>();
            var exportSectionRegistar = new Dictionary<string, Func<Task<int>>>();

            Options.CancelToken.Token.ThrowIfCancellationRequested();

            exportSectionRegistar.Add("Indexes", async () =>
            {
                Operations.ShowProgress("Begin reading indexes");
                var indexCount = await ImportIndexes(jsonReader);
                Operations.ShowProgress(string.Format("Done with reading indexes, total: {0}", indexCount));
                return indexCount;
            });

            exportSectionRegistar.Add("Docs", async () =>
            {
                Operations.ShowProgress("Begin reading documents");
                var documentCount = await ImportDocuments(jsonReader);
                Operations.ShowProgress(string.Format("Done with reading documents, total: {0}", documentCount));
                return documentCount;
            });

            exportSectionRegistar.Add("Attachments", async () =>
            {
                Operations.ShowProgress("Begin reading attachments");
                var attachmentCount = await ImportAttachments(importOptions.To, jsonReader);
                Operations.ShowProgress(string.Format("Done with reading attachments, total: {0}", attachmentCount));
                return attachmentCount;
            });

            exportSectionRegistar.Add("Transformers", async () =>
            {
                Operations.ShowProgress("Begin reading transformers");
                var transformersCount = await ImportTransformers(jsonReader);
                Operations.ShowProgress(string.Format("Done with reading transformers, total: {0}", transformersCount));
                return transformersCount;
            });

            exportSectionRegistar.Add("DocsDeletions", async () =>
            {
                Operations.ShowProgress("Begin reading deleted documents");
                var deletedDocumentsCount = await ImportDeletedDocuments(jsonReader);
                Operations.ShowProgress(string.Format("Done with reading deleted documents, total: {0}", deletedDocumentsCount));
                return deletedDocumentsCount;
            });

            exportSectionRegistar.Add("AttachmentsDeletions", async () =>
            {
                Operations.ShowProgress("Begin reading deleted attachments");
                var deletedAttachmentsCount = await ImportDeletedAttachments(jsonReader);
                Operations.ShowProgress(string.Format("Done with reading deleted attachments, total: {0}", deletedAttachmentsCount));
                return deletedAttachmentsCount;
            });

            exportSectionRegistar.Add("Identities", async () =>
            {
                Operations.ShowProgress("Begin reading identities");
                var identitiesCount = await ImportIdentities(jsonReader);
                Operations.ShowProgress(string.Format("Done with reading identities, total: {0}", identitiesCount));
                return identitiesCount;
            });

            exportSectionRegistar.Keys.ForEach(k => exportCounts[k] = 0);

            await RunSingleImportAsync(jsonReader, exportSectionRegistar, exportCounts);

            sw.Stop();

            Operations.ShowProgress("Imported {0:#,#;;0} documents and {1:#,#;;0} attachments, deleted {2:#,#;;0} documents and {3:#,#;;0} attachments in {4:#,#.###;;0} s", exportCounts["Docs"], exportCounts["Attachments"], exportCounts["DocsDeletions"], exportCounts["AttachmentsDeletions"], sw.ElapsedMilliseconds / 1000f);

            Options.CancelToken.Token.ThrowIfCancellationRequested();
        }

        private async Task RunSingleImportAsync(JsonReader jsonReader, IReadOnlyDictionary<string, Func<Task<int>>> exportSectionRegistar, IDictionary<string, int> exportCounts)
        {
            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndObject)
            {
                Options.CancelToken.Token.ThrowIfCancellationRequested();

                if (jsonReader.TokenType != JsonToken.PropertyName)
                {
                    throw new InvalidDataException("PropertyName was expected");
                }
                Func<Task<int>> currentAction;
                var currentSection = jsonReader.Value.ToString();
                if (exportSectionRegistar.TryGetValue(currentSection, out currentAction) == false)
                {
                    throw new InvalidDataException("Unexpected property found: " + jsonReader.Value);
                }
                if (jsonReader.Read() == false)
                {
                    exportCounts[currentSection] = 0;
                    continue;
                }

                if (jsonReader.TokenType != JsonToken.StartArray)
                {
                    throw new InvalidDataException("StartArray was expected");
                }

                exportCounts[currentSection] = await currentAction();
            }
        }

        private async Task<int> ImportIdentities(JsonTextReader jsonReader)
        {
            var count = 0;

            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
            {
                Options.CancelToken.Token.ThrowIfCancellationRequested();

                var identity = RavenJToken.ReadFrom(jsonReader);

                var identityName = identity.Value<string>("Key");

                if (FilterIdentity(identityName, Options.OperateOnTypes) == false)
                    continue;

                try
                {
                    await Operations.SeedIdentityFor(identityName, identity.Value<long>("Value"));
                }
                catch (Exception e)
                {
                    if (IgnoreErrorsAndContinue == false)
                        throw;

                    Operations.ShowProgress("Failed seeding identity for {0}. Message: {1}", identityName, e.Message);
                    continue;
                }

                count++;
            }

            await Operations.SeedIdentityFor(null, -1); // force flush

            return count;
        }

        private async Task<int> ImportDeletedDocuments(JsonReader jsonReader)
        {
            var count = 0;

            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
            {
                Options.CancelToken.Token.ThrowIfCancellationRequested();

                var item = RavenJToken.ReadFrom(jsonReader);

                var deletedDocumentInfo = new JsonSerializer { Converters = DefaultConverters }
                                                    .Deserialize<Tombstone>(new RavenJTokenReader(item));

                Operations.ShowProgress("Importing deleted document {0}", deletedDocumentInfo.Key);

                try
                {
                    await Operations.DeleteDocument(deletedDocumentInfo.Key);
                }
                catch (Exception e)
                {
                    if (IgnoreErrorsAndContinue == false)
                        throw;

                    Operations.ShowProgress("IMPORT of an deleted document {0} failed. Message: {1}", deletedDocumentInfo.Key, e.Message);
                }

                count++;
            }

            return count;
        }

        [Obsolete("Use RavenFS instead.")]
        private async Task<int> ImportDeletedAttachments(JsonReader jsonReader)
        {
            var count = 0;

            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
            {
                Options.CancelToken.Token.ThrowIfCancellationRequested();

                var item = RavenJToken.ReadFrom(jsonReader);

                var deletedAttachmentInfo = new JsonSerializer { Converters = DefaultConverters }
                                                    .Deserialize<Tombstone>(new RavenJTokenReader(item));

                Operations.ShowProgress("Importing deleted attachment {0}", deletedAttachmentInfo.Key);

                try
                {
                    await Operations.DeleteAttachment(deletedAttachmentInfo.Key);
                }
                catch (Exception e)
                {
                    if (IgnoreErrorsAndContinue == false)
                        throw;

                    Operations.ShowProgress("IMPORT of an deleted attachment {0} failed. Message: {1}", deletedAttachmentInfo.Key, e.Message);
                }

                count++;
            }

            return count;
        }

        private async Task<int> ImportTransformers(JsonTextReader jsonReader)
        {
            var count = 0;

            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
            {
                Options.CancelToken.Token.ThrowIfCancellationRequested();

                var transformer = RavenJToken.ReadFrom(jsonReader);
                if ((Options.OperateOnTypes & ItemType.Transformers) != ItemType.Transformers)
                    continue;

                var transformerName = transformer.Value<string>("name");

                try
                {
                    await Operations.PutTransformer(transformerName, transformer);
                }
                catch (Exception e)
                {
                    if (IgnoreErrorsAndContinue == false)
                        throw;

                    Operations.ShowProgress("PUT of a transformer {0} failed. Message: {1}", transformerName, e.Message);
                }

                count++;
            }

            await Operations.PutTransformer(null, null); // force flush

            return count;
        }

        private static readonly Lazy<JsonConverterCollection> defaultConverters = new Lazy<JsonConverterCollection>(() =>
        {
            var converters = new JsonConverterCollection()
            {
                new JsonToJsonConverter(),
                new StreamFromJsonConverter()
            };
            converters.Freeze();

            return converters;
        }, true);

        private static JsonConverterCollection DefaultConverters
        {
            get { return defaultConverters.Value; }
        }

        [Obsolete("Use RavenFS instead.")]
        private async Task<int> ImportAttachments(RavenConnectionStringOptions dst, JsonTextReader jsonReader)
        {
            var count = 0;

            while (true)
            {
                if (jsonReader.Read() == false)
                    throw new EndOfStreamException();
                if (jsonReader.TokenType == JsonToken.EndArray)
                    break;
                ValidateStartObject(jsonReader);

                if (jsonReader.Read() == false)
                    throw new EndOfStreamException();

                ValidatePropertyName(jsonReader, "Data");
                using (var valueStream = jsonReader.ReadBytesAsStream())
                {
                    if (jsonReader.Read() == false)
                        throw new EndOfStreamException();
                    ValidatePropertyName(jsonReader, "Metadata");

                    if (jsonReader.Read() == false) //go to StartObject token
                        throw new EndOfStreamException();
                    ValidateStartObject(jsonReader);

                    var metadata = (RavenJObject)RavenJToken.ReadFrom(jsonReader); //read the property as the object

                    if (jsonReader.Read() == false)
                        throw new EndOfStreamException();
                    ValidatePropertyName(jsonReader, "Key");

                    var key = jsonReader.ReadAsString();

                    if (jsonReader.Read() == false)
                        throw new EndOfStreamException();
                    if (jsonReader.TokenType == JsonToken.PropertyName)
                {
                        ValidatePropertyName(jsonReader, "Etag");
                        if (jsonReader.Read() == false) // read the etag value
                            throw new EndOfStreamException();
                        if (jsonReader.Read() == false) // consume the etag value...
                            throw new EndOfStreamException();

                }
                    ValidateEndObject(jsonReader);

                    if ((Operations.Options.OperateOnTypes & ItemType.Attachments) !=
                            ItemType.Attachments)
                        continue;

                    Operations.ShowProgress("Importing attachment {0}", key);
                    if (Operations.Options.StripReplicationInformation)
                {
                        metadata.Remove(Constants.RavenReplicationSource);
                        metadata.Remove(Constants.RavenReplicationVersion);
                    }

                    await Operations.PutAttachment(dst, new AttachmentExportInfo
                    {
                        Key = key,
                        Metadata = metadata,
                        Data = valueStream
                    });
                }
                count++;
            }

            await Operations.PutAttachment(dst, null); // force flush

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateStartObject(JsonTextReader jsonReader)
        {
            if (jsonReader.TokenType != JsonToken.StartObject)
                throw new InvalidOperationException("Expected StartObject token, but got " + jsonReader.TokenType +
                                                    ". The specific attachment format is invalid.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateEndObject(JsonTextReader jsonReader)
        {
            if (jsonReader.TokenType != JsonToken.EndObject)
                throw new InvalidOperationException("Expected EndObject token, but got " + jsonReader.TokenType +
                                                    ". The specific attachment format is invalid.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidatePropertyName(JsonTextReader jsonReader, string propName)
        {
            if (jsonReader.TokenType != JsonToken.PropertyName)
                throw new InvalidOperationException("Expected property '" + propName + "', but found unexpected token - " + jsonReader.TokenType);

            if (jsonReader.TokenType == JsonToken.PropertyName)
            {
                var propertyName = jsonReader.Value as string;
                if (!string.Equals(propertyName, propName))
                    throw new InvalidOperationException("Expected property token with the name 'Metadata', but found " + propertyName);
            }
        }

        private async Task<int> ImportDocuments(JsonTextReader jsonReader)
        {
            var now = SystemTime.UtcNow;
            var count = 0;
            string continuationDocId = "Raven/Smuggler/Continuation/" + Options.ContinuationToken;

            var state = new OperationState
            {
                FilePath = Options.ContinuationToken,
                LastDocsEtag = Options.StartDocsEtag,
            };

            JsonDocument lastEtagsDocument = null;

            try
            {
                if (Options.UseContinuationFile)
                {
                    lastEtagsDocument = Operations.GetDocument(continuationDocId);
                    if (lastEtagsDocument == null)
                    {
                        lastEtagsDocument = new JsonDocument()
                        {
                            Key = continuationDocId,
                            Etag = Etag.Empty,
                            DataAsJson = RavenJObject.FromObject(state)
                        };
                    }
                    else
                    {
                        state = lastEtagsDocument.DataAsJson.JsonDeserialization<OperationState>();
                    }

                    JsonDocument.EnsureIdInMetadata(lastEtagsDocument);
                }
            }
            catch (Exception e)
            {
                if (IgnoreErrorsAndContinue == false)
                    throw;

                Operations.ShowProgress("Failed loading continuation state. Message: {0}", e.Message);
            }

            int skippedDocuments = 0;
            long skippedDocumentsSize = 0;

            var tempLastEtag = Etag.Empty;

            var affectedCollections = new List<string>();
            Options.Filters.ForEach(filter =>
            {
                if (string.Equals(filter.Path, "@metadata.Raven-Entity-Name", StringComparison.OrdinalIgnoreCase))
                {
                    filter.Values.ForEach(affectedCollections.Add);
                }
            });

            var timeSinceLastWrite = Stopwatch.StartNew();

            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
            {
                Options.CancelToken.Token.ThrowIfCancellationRequested();

                if (timeSinceLastWrite.Elapsed > Options.HeartbeatLatency)
                {
                    var buildSkipDocument = BuildSkipDocument();
                    var heartbeatDocSize = (int)DocumentHelpers.GetRoughSize(buildSkipDocument);
                    await Operations.PutDocument(buildSkipDocument, heartbeatDocSize);
                    timeSinceLastWrite.Restart();
                }

                try
                {
                    var document = (RavenJObject)RavenJToken.ReadFrom(jsonReader);
                    var size = DocumentHelpers.GetRoughSize(document);
                    if (size > 1024 * 1024)
                    {
                        Operations.ShowProgress("Large document warning: {0:#,#.##;;0} kb - {1}",
                            (double)size / 1024,
                            document["@metadata"].Value<string>("@id"));
                    }
                    if ((Options.OperateOnTypes & ItemType.Documents) != ItemType.Documents)
                        continue;

                    if (Options.MatchFilters(document) == false)
                    {
                        if (affectedCollections.Count <= 0)
                            continue;

                        if (document.ContainsKey("@metadata") == false)
                            continue;

                        var key = document["@metadata"].Value<string>("@id");
                        if (key == null || key.StartsWith("Raven/Hilo/", StringComparison.OrdinalIgnoreCase) == false || affectedCollections.Any(x => key.EndsWith("/" + x, StringComparison.OrdinalIgnoreCase)) == false)
                            continue;
                    }

                    if (Options.ShouldExcludeExpired && Options.ExcludeExpired(document, now))
                        continue;

                    if (!string.IsNullOrEmpty(Options.TransformScript))
                        document = await Operations.TransformDocument(document, Options.TransformScript);

                    // If document is null after a transform we skip it. 
                    if (document == null)
                        continue;

                    var metadata = document["@metadata"] as RavenJObject;
                    if (metadata != null)
                    {
                        if (Options.SkipConflicted && metadata.ContainsKey(Constants.RavenReplicationConflictDocument))
                            continue;

                        if (Options.StripReplicationInformation)
                            document["@metadata"] = Operations.StripReplicationInformationFromMetadata(metadata);

                        if (Options.ShouldDisableVersioningBundle)
                            document["@metadata"] = SmugglerHelper.DisableVersioning(metadata);

                        document["@metadata"] = SmugglerHelper.HandleConflictDocuments(metadata);
                    }

                    if (Options.UseContinuationFile)
                    {
                        tempLastEtag = Etag.Parse(document.Value<RavenJObject>("@metadata").Value<string>("@etag"));
                        if (tempLastEtag.CompareTo(state.LastDocsEtag) <= 0) // tempLastEtag < lastEtag therefore we are skipping.
                        {
                            skippedDocuments++;
                            skippedDocumentsSize += size;

                            continue;
                        }
                    }

                    timeSinceLastWrite.Restart();
                    await Operations.PutDocument(document, (int)size);
                }
                catch (Exception e)
                {
                    if (IgnoreErrorsAndContinue == false)
                        throw;

                    Operations.ShowProgress("Failed to import document. Message: {0}", e.Message);
                }
                count++;

                if (count % Options.BatchSize == 0)
                {
                    if (Options.UseContinuationFile)
                    {
                        if (tempLastEtag.CompareTo(state.LastDocsEtag) > 0)
                            state.LastDocsEtag = tempLastEtag;

                        await WriteLastEtagToDatabase(state, lastEtagsDocument);
                    }

                    // Wait for the batch to be indexed before continue.
                    if (Options.WaitForIndexing)
                        await WaitForIndexingAsOfLastWrite();

                    Operations.ShowProgress("Read {0:#,#;;0} documents", count + skippedDocuments);
                }
            }

            if (Options.UseContinuationFile)
            {
                if (tempLastEtag.CompareTo(state.LastDocsEtag) > 0)
                    state.LastDocsEtag = tempLastEtag;

                await WriteLastEtagToDatabase(state, lastEtagsDocument);

                Operations.ShowProgress("Documents skipped by continuation {0:#,#;;0} - approx. {1:#,#.##;;0} Mb.", skippedDocuments, (double)skippedDocumentsSize / 1024 / 1024);
            }

            //precaution:
            //delete the heartbeat document in case the server is older version and doesn't recognize it
            await Operations.DeleteDocument(Constants.BulkImportHeartbeatDocKey);

            await Operations.PutDocument(null, -1); // force flush    

            return count;
        }


        private static RavenJObject BuildSkipDocument()
        {
            var metadata = new RavenJObject();
            metadata.Add("@id", Constants.BulkImportHeartbeatDocKey);
            var skipDoc = new JsonDocument
            {
                Key = Constants.BulkImportHeartbeatDocKey,
                DataAsJson = RavenJObject.FromObject(new
                {
                    LastHearbeatSent = SystemTime.UtcNow
                }),
                Metadata = metadata
            };

            return skipDoc.ToJson();
        }

        private async Task WriteLastEtagToDatabase(OperationState state, JsonDocument lastEtagsDocument)
        {
            lastEtagsDocument.DataAsJson = RavenJObject.FromObject(state);

            var stateDocument = lastEtagsDocument.ToJson();
            int stateDocumentSize = (int)DocumentHelpers.GetRoughSize(stateDocument);
            await Operations.PutDocument(stateDocument, stateDocumentSize);
        }

        private async Task<int> ImportIndexes(JsonReader jsonReader)
        {
            var count = 0;

            while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
            {
                Options.CancelToken.Token.ThrowIfCancellationRequested();

                var index = (RavenJObject)RavenJToken.ReadFrom(jsonReader);
                if ((Options.OperateOnTypes & ItemType.Indexes) != ItemType.Indexes)
                    continue;

                var indexName = index.Value<string>("name");
                if (indexName.StartsWith("Temp/"))
                    continue;

                var definition = index.Value<RavenJObject>("definition");
                if (definition.Value<bool>("IsCompiled"))
                    continue; // can't import compiled indexes

                if ((Options.OperateOnTypes & ItemType.RemoveAnalyzers) == ItemType.RemoveAnalyzers)
                {
                    definition.Remove("Analyzers");
                }

                try
                {
                    await Operations.PutIndex(indexName, index);
                }
                catch (Exception e)
                {
                    if (IgnoreErrorsAndContinue == false)
                        throw;

                    Operations.ShowProgress("Failed to import index {0}. Message: {1}", indexName, e.Message);
                }

                count++;
            }

            await Operations.PutIndex(null, null);

            return count;
        }

        private async Task ExportIndexes(RavenConnectionStringOptions src, JsonTextWriter jsonWriter)
        {
            var totalCount = 0;
            int retries = RetriesCount;

            while (true)
            {
                RavenJArray indexes;

                try
                {
                    indexes = await Operations.GetIndexes(src, totalCount);
                }
                catch (Exception e)
                {
                    if (retries-- == 0 && IgnoreErrorsAndContinue)
                    {
                        Operations.ShowProgress("Failed getting indexes too much times, stopping the index export entirely. Message: {0}", e.Message);
                        return;
                    }

                    if (IgnoreErrorsAndContinue == false)
                        throw new SmugglerExportException(e.Message, e);

                    Operations.ShowProgress("Failed fetching indexes. {0} retries remaining. Message: {1}", retries, e.Message);
                    continue;
                }

                if (indexes.Length == 0)
                {
                    Operations.ShowProgress("Done with reading indexes, total: {0}", totalCount);
                    break;
                }
                totalCount += indexes.Length;
                Operations.ShowProgress("Reading batch of {0,3} indexes, read so far: {1,10:#,#;;0}", indexes.Length, totalCount);
                foreach (var index in indexes)
                {
                    try
                    {
                        index.WriteTo(jsonWriter);
                    }
                    catch (Exception e)
                    {
                        if (IgnoreErrorsAndContinue == false)
                            throw new SmugglerExportException(e.Message, e);

                        Operations.ShowProgress("Failed to export index {0}. Message: {1}", index, e.Message);
                    }
                }
            }
        }

        private async Task DetectServerSupportedFeatures(RavenConnectionStringOptions server)
        {
            var serverVersion = await Operations.GetVersion(server);
            if (string.IsNullOrEmpty(serverVersion))
            {
                SetLegacyMode();
                return;
            }


            var customAttributes = typeof(SmugglerDatabaseApiBase).Assembly.GetCustomAttributes(false);
            dynamic versionAtt = customAttributes.Single(x => x.GetType().Name == "RavenVersionAttribute");
            var intServerVersion = int.Parse(versionAtt.Version.Replace(".", ""));


            if (intServerVersion < 25)
            {
                IsTransformersSupported = false;
                IsDocsStreamingSupported = false;
                IsIdentitiesSmugglingSupported = false;
                Operations.ShowProgress("Running in legacy mode, importing/exporting transformers is not supported. Server version: {0}. Smuggler version: {1}.", serverVersion, versionAtt.Version);
                return;
            }

            if (intServerVersion == 25)
            {
                Operations.ShowProgress("Running in legacy mode, importing/exporting identities is not supported. Server version: {0}. Smuggler version: {1}.", serverVersion, versionAtt.Version);

                IsTransformersSupported = true;
                IsDocsStreamingSupported = true;
                IsIdentitiesSmugglingSupported = false;

                return;
            }


            IsTransformersSupported = true;
            IsDocsStreamingSupported = true;
            IsIdentitiesSmugglingSupported = true;
        }

        private void SetLegacyMode()
        {
            IsTransformersSupported = false;
            IsDocsStreamingSupported = false;
            IsIdentitiesSmugglingSupported = false;
            Operations.ShowProgress("Server version is not available. Running in legacy mode which does not support transformers.");
        }

        public bool IsTransformersSupported { get; private set; }
        public bool IsDocsStreamingSupported { get; private set; }
        public bool IsIdentitiesSmugglingSupported { get; private set; }
    }

    public class ExportOperationStatus
    {
        public Etag LastEtag { get; set; }

        public int NumberOfExportedItems { get; set; }
    }
}
#endif