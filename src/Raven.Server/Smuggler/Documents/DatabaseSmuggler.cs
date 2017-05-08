﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Raven.Client;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Smuggler;
using Raven.Client.Util;
using Raven.Server.Documents;
using Raven.Server.Documents.Indexes.Auto;
using Raven.Server.Documents.Indexes.MapReduce.Auto;
using Raven.Server.Smuggler.Documents.Data;
using Raven.Server.Smuggler.Documents.Processors;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Smuggler.Documents
{
    public class DatabaseSmuggler
    {
        private readonly ISmugglerSource _source;
        private readonly ISmugglerDestination _destination;
        private readonly DatabaseSmugglerOptions _options;
        private readonly SmugglerResult _result;
        private readonly SystemTime _time;
        private readonly Action<IOperationProgress> _onProgress;
        private readonly SmugglerPatcher _patcher;
        private CancellationToken _token;
        private HashSet<LazyStringValue> _attachmentStreamsAlreadyExported;

        public DatabaseSmuggler(
            ISmugglerSource source,
            ISmugglerDestination destination,
            SystemTime time,
            DatabaseSmugglerOptions options = null,
            SmugglerResult result = null,
            Action<IOperationProgress> onProgress = null,
            CancellationToken token = default(CancellationToken))
        {
            _source = source;
            _destination = destination;
            _options = options ?? new DatabaseSmugglerOptions();
            _result = result;
            _token = token;

            if (string.IsNullOrWhiteSpace(_options.TransformScript) == false)
                _patcher = new SmugglerPatcher(_options);

            _time = time;
            _onProgress = onProgress ?? (progress => { });
        }

        public SmugglerResult Execute()
        {
            var result = _result ?? new SmugglerResult();

            long buildVersion;
            using (_source.Initialize(_options, result, out buildVersion))
            using (_destination.Initialize(_options, result, buildVersion))
            {
                var buildType = BuildVersion.Type(buildVersion);
                var currentType = _source.GetNextType();
                while (currentType != DatabaseItemType.None)
                {
                    ProcessType(currentType, result, buildType);

                    currentType = _source.GetNextType();
                }

                EnsureStepProcessed(result.Documents);
                EnsureStepProcessed(result.RevisionDocuments);
                EnsureStepProcessed(result.Indexes);
                EnsureStepProcessed(result.Transformers);
                EnsureStepProcessed(result.Identities);

                return result;
            }
        }

        private static void EnsureStepProcessed(SmugglerProgressBase.Counts counts)
        {
            if (counts.Processed)
                return;

            counts.Processed = true;
            counts.Skipped = true;
        }

        private void ProcessType(DatabaseItemType type, SmugglerResult result, BuildVersionType buildType)
        {
            if ((_options.OperateOnTypes & type) != type)
            {
                SkipType(type, result);
                return;
            }

            result.AddInfo($"Started processing {type}.");
            _onProgress.Invoke(result.Progress);

            SmugglerProgressBase.Counts counts;
            switch (type)
            {
                case DatabaseItemType.Documents:
                    counts = ProcessDocuments(result, buildType);
                    break;
                case DatabaseItemType.RevisionDocuments:
                    counts = ProcessRevisionDocuments(result);
                    break;
                case DatabaseItemType.Indexes:
                    counts = ProcessIndexes(result);
                    break;
                case DatabaseItemType.Transformers:
                    counts = ProcessTransformers(result);
                    break;
                case DatabaseItemType.Identities:
                    counts = ProcessIdentities(result);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            counts.Processed = true;
            result.AddInfo($"Finished processing {type}. {counts}");
            _onProgress.Invoke(result.Progress);
        }

        private void SkipType(DatabaseItemType type, SmugglerResult result)
        {
            result.AddInfo($"Skipping '{type}' processing.");
            _onProgress.Invoke(result.Progress);

            var numberOfItemsSkipped = _source.SkipType(type);

            SmugglerProgressBase.Counts counts;
            switch (type)
            {
                case DatabaseItemType.Documents:
                    counts = result.Documents;
                    break;
                case DatabaseItemType.RevisionDocuments:
                    counts = result.RevisionDocuments;
                    break;
                case DatabaseItemType.Indexes:
                    counts = result.Indexes;
                    break;
                case DatabaseItemType.Transformers:
                    counts = result.Transformers;
                    break;
                case DatabaseItemType.Identities:
                    counts = result.Identities;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            counts.Skipped = true;
            counts.Processed = true;

            if (numberOfItemsSkipped > 0)
            {
                counts.ReadCount = numberOfItemsSkipped;
                result.AddInfo($"Skipped '{type}' processing. Skipped {numberOfItemsSkipped} items.");
            }
            else
                result.AddInfo($"Skipped '{type}' processing.");

            _onProgress.Invoke(result.Progress);
        }

        private SmugglerProgressBase.Counts ProcessIdentities(SmugglerResult result)
        {
            using (var actions = _destination.Identities())
            {
                foreach (var kvp in _source.GetIdentities())
                {
                    _token.ThrowIfCancellationRequested();
                    result.Identities.ReadCount++;

                    if (kvp.Equals(default(KeyValuePair<string, long>)))
                    {
                        result.Identities.ErroredCount++;
                        continue;
                    }

                    try
                    {
                        actions.WriteIdentity(kvp.Key, kvp.Value);
                    }
                    catch (Exception e)
                    {
                        result.Identities.ErroredCount++;
                        result.AddError($"Could not write identity '{kvp.Key} {kvp.Value}': {e.Message}");
                    }
                }
            }

            return result.Identities;
        }

        private SmugglerProgressBase.Counts ProcessTransformers(SmugglerResult result)
        {
            using (var actions = _destination.Transformers())
            {
                foreach (var transformer in _source.GetTransformers())
                {
                    _token.ThrowIfCancellationRequested();
                    result.Transformers.ReadCount++;

                    if (transformer == null)
                    {
                        result.Transformers.ErroredCount++;
                        continue;
                    }

                    try
                    {
                        actions.WriteTransformer(transformer);
                    }
                    catch (Exception e)
                    {
                        result.Transformers.ErroredCount++;
                        result.AddError($"Could not write transformer '{transformer.Name}': {e.Message}");
                    }
                }
            }

            return result.Transformers;
        }

        private SmugglerProgressBase.Counts ProcessIndexes(SmugglerResult result)
        {
            using (var actions = _destination.Indexes())
            {
                foreach (var index in _source.GetIndexes())
                {
                    _token.ThrowIfCancellationRequested();
                    result.Indexes.ReadCount++;

                    if (index == null)
                    {
                        result.Indexes.ErroredCount++;
                        continue;
                    }

                    switch (index.Type)
                    {
                        case IndexType.AutoMap:
                            var autoMapIndexDefinition = (AutoMapIndexDefinition)index.IndexDefinition;

                            try
                            {
                                actions.WriteIndex(autoMapIndexDefinition, IndexType.AutoMap);
                            }
                            catch (Exception e)
                            {
                                result.Indexes.ErroredCount++;
                                result.AddError($"Could not write auto map index '{autoMapIndexDefinition.Name}': {e.Message}");
                            }
                            break;
                        case IndexType.AutoMapReduce:
                            var autoMapReduceIndexDefinition = (AutoMapReduceIndexDefinition)index.IndexDefinition;
                            try
                            {
                                actions.WriteIndex(autoMapReduceIndexDefinition, IndexType.AutoMapReduce);
                            }
                            catch (Exception e)
                            {
                                result.Indexes.ErroredCount++;
                                result.AddError($"Could not write auto map-reduce index '{autoMapReduceIndexDefinition.Name}': {e.Message}");
                            }
                            break;
                        case IndexType.Map:
                        case IndexType.MapReduce:
                            var indexDefinition = (IndexDefinition)index.IndexDefinition;
                            if (string.Equals(indexDefinition.Name, "Raven/DocumentsByEntityName", StringComparison.OrdinalIgnoreCase))
                            {
                                result.AddInfo("Skipped 'Raven/DocumentsByEntityName' index. It is no longer needed.");
                                continue;
                            }

                            if (string.Equals(indexDefinition.Name, "Raven/ConflictDocuments", StringComparison.OrdinalIgnoreCase))
                            {
                                result.AddInfo("Skipped 'Raven/ConflictDocuments' index. It is no longer needed.");
                                continue;
                            }

                            try
                            {
                                if (_options.RemoveAnalyzers)
                                {
                                    foreach (var indexDefinitionField in indexDefinition.Fields)
                                        indexDefinitionField.Value.Analyzer = null;
                                }

                                actions.WriteIndex(indexDefinition);
                            }
                            catch (Exception e)
                            {
                                result.Indexes.ErroredCount++;
                                result.AddError($"Could not write index '{indexDefinition.Name}': {e.Message}");
                            }
                            break;
                        case IndexType.Faulty:
                            break;
                        default:
                            throw new NotSupportedException(index.Type.ToString());
                    }
                }
            }

            return result.Indexes;
        }

        private SmugglerProgressBase.Counts ProcessRevisionDocuments(SmugglerResult result)
        {
            using (var actions = _destination.RevisionDocuments())
            {
                foreach (var document in _source.GetRevisionDocuments(_options.CollectionsToExport, actions, _options.RevisionDocumentsLimit ?? int.MaxValue))
                {
                    _token.ThrowIfCancellationRequested();
                    result.RevisionDocuments.ReadCount++;

                    if (result.RevisionDocuments.ReadCount % 1000 == 0)
                    {
                        result.AddInfo($"Read {result.RevisionDocuments.ReadCount:#,#;;0} documents.");
                        _onProgress.Invoke(result.Progress);
                    }

                    if (document == null)
                    {
                        result.RevisionDocuments.ErroredCount++;
                        continue;
                    }

                    Debug.Assert(document.Key != null);

                    WriteAttachments(document, actions);

                    document.NonPersistentFlags |= NonPersistentDocumentFlags.FromSmuggler;

                    actions.WriteDocument(document);

                    result.RevisionDocuments.LastEtag = document.Etag;
                }
            }

            return result.RevisionDocuments;
        }

        private SmugglerProgressBase.Counts ProcessDocuments(SmugglerResult result, BuildVersionType buildType)
        {
            using (var actions = _destination.Documents())
            {
                foreach (var doc in _source.GetDocuments(_options.CollectionsToExport, actions))
                {
                    _token.ThrowIfCancellationRequested();
                    result.Documents.ReadCount++;

                    if (result.Documents.ReadCount % 1000 == 0)
                    {
                        result.AddInfo($"Read {result.Documents.ReadCount:#,#;;0} documents.");
                        _onProgress.Invoke(result.Progress);
                    }

                    if (doc == null)
                    {
                        result.Documents.ErroredCount++;
                        continue;
                    }

                    if (doc.Key == null)
                        ThrowInvalidData();

                    var document = doc;

                    if (CanSkipDocument(document, buildType))
                    {
                        result.Documents.SkippedCount++;
                        continue;
                    }

                    if (_options.IncludeExpired == false && document.Expired(_time.GetUtcNow()))
                    {
                        result.Documents.SkippedCount++;
                        continue;
                    }

                    WriteAttachments(document, actions);

                    if (_patcher != null)
                    {
                        document = _patcher.Transform(document, actions.GetContextForNewDocument());
                        if (document == null)
                        {
                            result.Documents.SkippedCount++;
                            continue;
                        }
                    }

                    document.NonPersistentFlags |= NonPersistentDocumentFlags.FromSmuggler;

                    actions.WriteDocument(document);

                    result.Documents.LastEtag = document.Etag;
                }
            }

            return result.Documents;
        }

        private void WriteAttachments(Document document, IDocumentActions actions)
        {
            var source = _source as DatabaseSource;
            var streamDestination = actions as StreamDestination.StreamDocumentActions;
            if (source == null || streamDestination == null)
                return;

            if ((document.Flags & DocumentFlags.HasAttachments) != DocumentFlags.HasAttachments || 
                document.Data.TryGet(Constants.Documents.Metadata.Key, out BlittableJsonReaderObject metadata) == false || 
                metadata.TryGet(Constants.Documents.Metadata.Attachments, out BlittableJsonReaderArray attachments) == false)
                return;

            if (_attachmentStreamsAlreadyExported == null)
                _attachmentStreamsAlreadyExported = new HashSet<LazyStringValue>();

            foreach (BlittableJsonReaderObject attachment in attachments)
            {
                if (attachment.TryGet(nameof(AttachmentResult.Hash), out LazyStringValue hash) == false)
                    throw new ArgumentException($"Hash field is mandatory in attachment's metadata: {attachment}");

                if (_attachmentStreamsAlreadyExported.Add(hash))
                {
                    using (var stream = source.GetAttachmentStream(hash))
                    {
                        streamDestination.WriteAttachmentStream(hash, stream);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanSkipDocument(Document document, BuildVersionType buildType)
        {
            if (buildType != BuildVersionType.V3)
                return false;

            // skipping "Raven/Replication/DatabaseIdsCache" and
            // "Raven/Replication/Sources/{GUID}"
            if (document.Key.Size != 34 && document.Key.Size != 62)
                return false;

            return document.Key == "Raven/Replication/DatabaseIdsCache" ||
                   document.Key.StartsWith("Raven/Replication/Sources/");
        }

        private static void ThrowInvalidData()
        {
            throw new InvalidDataException("Document does not contain an id.");
        }
    }
}