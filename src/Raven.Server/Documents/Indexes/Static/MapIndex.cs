﻿using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client;
using Raven.Client.Documents.Changes;
using Raven.Client.Documents.Indexes;
using Raven.Client.Extensions;
using Raven.Server.Documents.Indexes.Configuration;
using Raven.Server.Documents.Indexes.Persistence.Lucene;
using Raven.Server.Documents.Indexes.Workers;
using Raven.Server.ServerWide.Context;
using Voron;

namespace Raven.Server.Documents.Indexes.Static
{
    public class MapIndex : MapIndexBase<MapIndexDefinition>
    {
        private readonly HashSet<string> _referencedCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected internal readonly StaticIndexBase _compiled;
        private bool? _isSideBySide;

        private HandleReferences _handleReferences;

        private MapIndex(long etag, MapIndexDefinition definition, StaticIndexBase compiled)
            : base(etag, IndexType.Map, definition)
        {
            _compiled = compiled;

            if (_compiled.ReferencedCollections == null)
                return;

            foreach (var collection in _compiled.ReferencedCollections)
            {
                foreach (var referencedCollection in collection.Value)
                    _referencedCollections.Add(referencedCollection.Name);
            }
        }

        public override bool HasBoostedFields => _compiled.HasBoostedFields;

        public override bool IsMultiMap => _compiled.Maps.Count > 1 || _compiled.Maps.Any(x => x.Value.Count > 1);

        protected override IIndexingWork[] CreateIndexWorkExecutors()
        {
            var workers = new List<IIndexingWork>();
            workers.Add(new CleanupDeletedDocuments(this, DocumentDatabase.DocumentsStorage, _indexStorage, Configuration, null));

            if (_referencedCollections.Count > 0)
                workers.Add(_handleReferences = new HandleReferences(this, _compiled.ReferencedCollections, DocumentDatabase.DocumentsStorage, _indexStorage, Configuration));

            workers.Add(new MapDocuments(this, DocumentDatabase.DocumentsStorage, _indexStorage, null, Configuration));

            return workers.ToArray();
        }

        public override void HandleDelete(DocumentTombstone tombstone, string collection, IndexWriteOperation writer, TransactionOperationContext indexContext, IndexingStatsScope stats)
        {
            if (_referencedCollections.Count > 0)
                _handleReferences.HandleDelete(tombstone, collection, writer, indexContext, stats);

            base.HandleDelete(tombstone, collection, writer, indexContext, stats);
        }

        protected override bool IsStale(DocumentsOperationContext databaseContext, TransactionOperationContext indexContext, long? cutoff = null)
        {
            var isStale = base.IsStale(databaseContext, indexContext, cutoff);
            if (isStale || _referencedCollections.Count == 0)
                return isStale;

            return StaticIndexHelper.IsStale(this, databaseContext, indexContext, cutoff);
        }

        protected override void HandleDocumentChange(DocumentChange change)
        {
            if (HandleAllDocs == false && Collections.Contains(change.CollectionName) == false && 
                _referencedCollections.Contains(change.CollectionName) == false)
                return;

            _mre.Set();
        }

        protected override unsafe long CalculateIndexEtag(bool isStale, DocumentsOperationContext documentsContext, TransactionOperationContext indexContext)
        {
            if (_referencedCollections.Count == 0)
                return base.CalculateIndexEtag(isStale, documentsContext, indexContext);

            var minLength = MinimumSizeForCalculateIndexEtagLength();
            var length = minLength +
                         sizeof(long) * 2 * (Collections.Count * _referencedCollections.Count); // last referenced collection etags and last processed reference collection etags

            var indexEtagBytes = stackalloc byte[length];

            CalculateIndexEtagInternal(indexEtagBytes, isStale, documentsContext, indexContext);

            var writePos = indexEtagBytes + minLength;

            return StaticIndexHelper.CalculateIndexEtag(this, length, indexEtagBytes, writePos, documentsContext, indexContext);
        }

        protected override bool ShouldReplace()
        {
            if (_isSideBySide.HasValue == false)
                _isSideBySide = Name.StartsWith(Constants.Documents.Indexing.SideBySideIndexNamePrefix, StringComparison.OrdinalIgnoreCase);

            if (_isSideBySide == false)
                return false;

            DocumentsOperationContext databaseContext;
            TransactionOperationContext indexContext;
            using (DocumentDatabase.DocumentsStorage.ContextPool.AllocateOperationContext(out databaseContext))
            using (_contextPool.AllocateOperationContext(out indexContext))
            {
                using (indexContext.OpenReadTransaction())
                using (databaseContext.OpenReadTransaction())
                {
                    var canReplace = StaticIndexHelper.CanReplace(this, IsStale(databaseContext, indexContext), DocumentDatabase, databaseContext, indexContext);
                    if (canReplace)
                        _isSideBySide = null;

                    return canReplace;
                }
            }
        }

        public override Dictionary<string, HashSet<CollectionName>> GetReferencedCollections()
        {
            return _compiled.ReferencedCollections;
        }

        public override IIndexedDocumentsEnumerator GetMapEnumerator(IEnumerable<Document> documents, string collection, TransactionOperationContext indexContext, IndexingStatsScope stats)
        {
            return new StaticIndexDocsEnumerator(documents, _compiled.Maps[collection], collection, stats);
        }

        public override Dictionary<string, long> GetLastProcessedDocumentTombstonesPerCollection()
        {
            TransactionOperationContext context;
            using (_contextPool.AllocateOperationContext(out context))
            {
                using (var tx = context.OpenReadTransaction())
                {
                    var etags = GetLastProcessedDocumentTombstonesPerCollection(tx);

                    if (_referencedCollections.Count <= 0)
                        return etags;

                    foreach (var collection in Collections)
                    {
                        HashSet<CollectionName> referencedCollections;
                        if (_compiled.ReferencedCollections.TryGetValue(collection, out referencedCollections) == false)
                            throw new InvalidOperationException("Should not happen ever!");

                        foreach (var referencedCollection in referencedCollections)
                        {
                            var etag = _indexStorage.ReadLastProcessedReferenceTombstoneEtag(tx, collection, referencedCollection);
                            long currentEtag;
                            if (etags.TryGetValue(referencedCollection.Name, out currentEtag) == false || etag < currentEtag)
                                etags[referencedCollection.Name] = etag;
                        }
                    }

                    return etags;
                }
            }
        }

        public static Index CreateNew(IndexDefinition definition, DocumentDatabase documentDatabase)
        {
            var instance = CreateIndexInstance(definition);
            instance.Initialize(documentDatabase,
                new SingleIndexConfiguration(definition.Configuration, documentDatabase.Configuration),
                documentDatabase.Configuration.PerformanceHints);

            return instance;
        }

        public static Index Open(long etag, StorageEnvironment environment, DocumentDatabase documentDatabase)
        {
            var definition = MapIndexDefinition.Load(environment);
            var instance = CreateIndexInstance(definition);

            instance.Initialize(environment, documentDatabase,
                new SingleIndexConfiguration(definition.Configuration, documentDatabase.Configuration),
                documentDatabase.Configuration.PerformanceHints);

            return instance;
        }

        public static void Update(Index index, IndexDefinition definition, DocumentDatabase documentDatabase)
        {
            var staticMapIndex = (MapIndex)index;
            var staticIndex = staticMapIndex._compiled;

            var staticMapIndexDefinition = new MapIndexDefinition(definition, staticIndex.Maps.Keys.ToHashSet(), staticIndex.OutputFields, staticIndex.HasDynamicFields);
            staticMapIndex.Update(staticMapIndexDefinition, new SingleIndexConfiguration(definition.Configuration, documentDatabase.Configuration));
        }

        private static MapIndex CreateIndexInstance(IndexDefinition definition)
        {
            var staticIndex = IndexAndTransformerCompilationCache.GetIndexInstance(definition);

            var staticMapIndexDefinition = new MapIndexDefinition(definition, staticIndex.Maps.Keys.ToHashSet(), staticIndex.OutputFields, staticIndex.HasDynamicFields);
            var instance = new MapIndex(definition.Etag, staticMapIndexDefinition, staticIndex);
            return instance;
        }
    }
}