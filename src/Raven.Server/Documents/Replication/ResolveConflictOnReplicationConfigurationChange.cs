﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.ServerWide;
using Raven.Server.Documents.Patch;
using Raven.Server.Documents.Revisions;
using Raven.Server.NotificationCenter.Notifications;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils;
using Sparrow.Json;
using Sparrow.Logging;
using Voron;

namespace Raven.Server.Documents.Replication
{
    public class ResolveConflictOnReplicationConfigurationChange
    {
        private readonly DocumentDatabase _database;
        private readonly Logger _log;
        private readonly ReplicationLoader _replicationLoader;

        public Task ResolveConflictsTask = Task.CompletedTask;

        internal Dictionary<string, ScriptResolver> ScriptConflictResolversCache = new Dictionary<string, ScriptResolver>();
        public ConflictSolver ConflictSolver => _replicationLoader.ConflictSolverConfig;

        public ResolveConflictOnReplicationConfigurationChange(ReplicationLoader replicationLoader, Logger log)
        {
            _replicationLoader = replicationLoader ??
                throw new ArgumentNullException($"{nameof(ResolveConflictOnReplicationConfigurationChange)} must have replicationLoader instance");
            _database = _replicationLoader.Database;
            _log = log;
        }

        public long ConflictsCount => _database.DocumentsStorage?.ConflictsStorage?.ConflictsCount ?? 0;

        public void RunConflictResolversOnce()
        {
            UpdateScriptResolvers();

            if (ConflictsCount > 0 && ConflictSolver?.IsEmpty() == false)
            {
                try
                {
                    ResolveConflictsTask.Wait();
                }
                catch (Exception e)
                {
                    if (_log.IsInfoEnabled)
                        _log.Info("Failed to wait for a previous task of automatic conflict resolution", e);
                }
                ResolveConflictsTask = Task.Run(ResolveConflictsInBackground);
            }
        }

        private async Task ResolveConflictsInBackground()
        {
            try
            {
                using (_database.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
                {
                    using (context.OpenReadTransaction())
                    {
                        var resolvedConflicts = new List<(DocumentConflict ResolvedConflict, long MaxConflictEtag)>();

                        var hadConflicts = false;

                        foreach (var conflicts in _database.DocumentsStorage.ConflictsStorage.GetAllConflictsBySameId(context))
                        {
                            if (_database.DatabaseShutdown.IsCancellationRequested)
                                break;

                            hadConflicts = true;

                            var collection = conflicts[0].Collection;

                            var maxConflictEtag = conflicts.Max(x => x.Etag);

                            DocumentConflict resolved;
                            if (ScriptConflictResolversCache.TryGetValue(collection, out var scriptResolver) && scriptResolver != null)
                            {
                                if (TryResolveConflictByScriptInternal(
                                    context,
                                    scriptResolver,
                                    conflicts,
                                    collection,
                                    resolvedConflict: out resolved))
                                {
                                    resolvedConflicts.Add((resolved, maxConflictEtag));

                                    //stats.AddResolvedBy(collection + " Script", conflictList.Count);
                                    continue;
                                }
                            }

                            if (ConflictSolver?.ResolveToLatest == true)
                            {
                                resolvedConflicts.Add((ResolveToLatest(context, conflicts), maxConflictEtag));

                                //stats.AddResolvedBy("ResolveToLatest", conflictList.Count);
                            }
                        }

                        if (hadConflicts == false || _database.DatabaseShutdown.IsCancellationRequested)
                            return;

                        if (resolvedConflicts.Count > 0)
                        {
                            var cmd = new PutResolvedConflictsCommand(_database.DocumentsStorage.ConflictsStorage, resolvedConflicts, this);
                            await _database.TxMerger.Enqueue(cmd);
                            if (cmd.RequiresRetry)
                                RunConflictResolversOnce();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (_log.IsInfoEnabled)
                    _log.Info("Failed to run automatic conflict resolution", e);
            }
        }

        private class PutResolvedConflictsCommand : TransactionOperationsMerger.MergedTransactionCommand
        {
            private readonly ConflictsStorage _conflictsStorage;
            private readonly List<(DocumentConflict ResolvedConflict, long MaxConflictEtag)> _resolvedConflicts;
            private readonly ResolveConflictOnReplicationConfigurationChange _resolver;
            public bool RequiresRetry;

            public PutResolvedConflictsCommand(ConflictsStorage conflictsStorage, List<(DocumentConflict, long)> resolvedConflicts, ResolveConflictOnReplicationConfigurationChange resolver)
            {
                _conflictsStorage = conflictsStorage;
                _resolvedConflicts = resolvedConflicts;
                _resolver = resolver;
            }

            public override int Execute(DocumentsOperationContext context)
            {
                var count = 0;

                foreach (var item in _resolvedConflicts)
                {
                    count++;

                    using (Slice.External(context.Allocator, item.ResolvedConflict.LowerId, out var slice))
                    {
                        // let's check if nothing has changed since we resolved the conflict in the read tx
                        // in particular the conflict could be resolved externally before the tx merger opened this write tx

                        if (_conflictsStorage.ShouldThrowConcurrencyExceptionOnConflict(context, slice, item.MaxConflictEtag, out var _))
                            continue;
                        RequiresRetry = true;
                    }

                    _resolver.PutResolvedDocument(context, item.ResolvedConflict);
                }

                return count;
            }
        }

        private void UpdateScriptResolvers()
        {
            if (ConflictSolver?.ResolveByCollection == null)
            {
                if (ScriptConflictResolversCache.Count > 0)
                    ScriptConflictResolversCache = new Dictionary<string, ScriptResolver>();
                return;
            }
            var copy = new Dictionary<string, ScriptResolver>();
            foreach (var kvp in ConflictSolver.ResolveByCollection)
            {
                var collection = kvp.Key;
                var script = kvp.Value.Script;
                if (string.IsNullOrEmpty(script.Trim()))
                {
                    continue;
                }
                copy[collection] = new ScriptResolver
                {
                    Script = script
                };
            }
            ScriptConflictResolversCache = copy;
        }

        private bool ValidatedResolveByScriptInput(ScriptResolver scriptResolver,
            IReadOnlyList<DocumentConflict> conflicts,
            LazyStringValue collection)
        {
            if (scriptResolver == null)
                return false;
            if (collection == null)
                return false;
            if (conflicts.Count < 2)
                return false;

            foreach (var documentConflict in conflicts)
            {
                if (collection != documentConflict.Collection)
                {
                    var msg = $"All conflicted documents must have same collection name, but we found conflicted document in {collection} and an other one in {documentConflict.Collection}";
                    if (_log.IsInfoEnabled)
                        _log.Info(msg);

                    var differentCollectionNameAlert = AlertRaised.Create(
                        $"Script unable to resolve conflicted documents with the ID {documentConflict.Id}",
                        msg,
                        AlertType.Replication,
                        NotificationSeverity.Error,
                        "Mismatched Collections On Replication Resolve"
                        );
                    _database.NotificationCenter.Add(differentCollectionNameAlert);
                    return false;
                }
            }

            return true;
        }

        public void PutResolvedDocument(
           DocumentsOperationContext context,
           DocumentConflict conflict)
        {
            if (conflict.Doc == null)
            {
                using (Slice.External(context.Allocator, conflict.LowerId, out Slice lowerId))
                {
                    _database.DocumentsStorage.Delete(context, lowerId, conflict.Id, null,
                        _database.Time.GetUtcNow().Ticks, conflict.ChangeVector, new CollectionName(conflict.Collection));
                    return;
                }
            }

            // because we are resolving to a conflict, and putting a document will
            // delete all the conflicts, we have to create a copy of the document
            // in order to avoid the data we are saving from being removed while
            // we are saving it

            // the resolved document could be an update of the existing document, so it's a good idea to clone it also before updating.
            using (var clone = conflict.Doc.Clone(context))
            {
                // handle the case where we resolve a conflict for a document from a different collection
                DeleteDocumentFromDifferentCollectionIfNeeded(context, conflict);

                ReplicationUtils.EnsureCollectionTag(clone, conflict.Collection);
                _database.DocumentsStorage.Put(context, conflict.LowerId, null, clone, null, conflict.ChangeVector, DocumentFlags.Resolved);
            }
        }

        private void DeleteDocumentFromDifferentCollectionIfNeeded(DocumentsOperationContext ctx, DocumentConflict conflict)
        {
            // if already conflicted, don't need to do anything
            var oldVersion = _database.DocumentsStorage.Get(ctx, conflict.LowerId, throwOnConflict: false);

            if (oldVersion == null)
                return;

            var oldVersionCollectionName = CollectionName.GetCollectionName(oldVersion.Data);
            if (oldVersionCollectionName.Equals(conflict.Collection, StringComparison.OrdinalIgnoreCase))
                return;

            _database.DocumentsStorage.DeleteWithoutCreatingTombstone(ctx, oldVersionCollectionName, oldVersion.StorageId, isTombstone: false);
        }

        public bool TryResolveConflictByScriptInternal(
            DocumentsOperationContext context,
            ScriptResolver scriptResolver,
            IReadOnlyList<DocumentConflict> conflicts,
            LazyStringValue collection,
            out DocumentConflict resolvedConflict)
        {
            resolvedConflict = null;

            if (ValidatedResolveByScriptInput(scriptResolver, conflicts, collection) == false)
                return false;

            var patch = new PatchConflict(_database, conflicts);
            var updatedConflict = conflicts[0];
            var patchRequest = new PatchRequest(scriptResolver.Script, PatchRequestType.Conflict);
            if (patch.TryResolveConflict(context, patchRequest, out BlittableJsonReaderObject resolved) == false)
            {
                return false;
            }

            updatedConflict.Doc = resolved;
            updatedConflict.Collection = collection;
            updatedConflict.ChangeVector = ChangeVectorUtils.MergeVectors(conflicts.Select(c => c.ChangeVector).ToList());

            resolvedConflict = updatedConflict;

            return true;
        }

        public DocumentConflict ResolveToLatest(DocumentsOperationContext context, List<DocumentConflict> conflicts)
        {
            // we have to sort this here because we need to ensure that all the nodes are always 
            // arrive to the same conclusion, regardless of what time they go it
            conflicts.Sort((x, y) => string.Compare(x.ChangeVector, y.ChangeVector, StringComparison.Ordinal));

            var latestDoc = conflicts[0];
            var latestTime = latestDoc.LastModified.Ticks;

            foreach (var documentConflict in conflicts)
            {
                if (documentConflict.LastModified.Ticks > latestTime)
                {
                    latestDoc = documentConflict;
                    latestTime = documentConflict.LastModified.Ticks;
                }
            }

            latestDoc.ChangeVector = ChangeVectorUtils.MergeVectors(conflicts.Select(c => c.ChangeVector).ToList());

            return latestDoc;
        }
    }
}
