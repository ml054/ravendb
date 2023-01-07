﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Backups;
using Raven.Client.Exceptions;
using Raven.Client.Extensions;
using Raven.Client.Http;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Sharding;
using Raven.Client.Util;
using Raven.Server.Config;
using Raven.Server.Config.Settings;
using Raven.Server.Json;
using Raven.Server.NotificationCenter;
using Raven.Server.NotificationCenter.Notifications;
using Raven.Server.Rachis;
using Raven.Server.ServerWide.Commands;
using Raven.Server.ServerWide.Commands.Indexes;
using Raven.Server.ServerWide.Commands.Sharding;
using Raven.Server.ServerWide.Context;
using Raven.Server.ServerWide.Maintenance.Sharding;
using Raven.Server.Utils;
using Sparrow;
using Sparrow.Json;

namespace Raven.Server.ServerWide.Maintenance
{
    internal class ClusterObserver : IDisposable
    {
        private readonly PoolOfThreads.LongRunningWork _observe;
        private readonly DatabaseTopologyUpdater _databaseTopologyUpdater;
        private readonly OrchestratorTopologyUpdater _orchestratorTopologyUpdater;
        private readonly CancellationTokenSource _cts;
        private readonly ClusterMaintenanceSupervisor _maintenance;
        private readonly string _nodeTag;
        private readonly RachisConsensus<ClusterStateMachine> _engine;
        private readonly ClusterContextPool _contextPool;
        private readonly ObserverLogger _observerLogger;

        private readonly TimeSpan _supervisorSamplePeriod;
        private readonly ServerStore _server;
        private readonly TimeSpan _stabilizationTime;
        private readonly long _stabilizationTimeMs;
        
        public SystemTime Time = new SystemTime();

        private ServerNotificationCenter NotificationCenter => _server.NotificationCenter;

        public ClusterObserver(
            ServerStore server,
            ClusterMaintenanceSupervisor maintenance,
            RachisConsensus<ClusterStateMachine> engine,
            long term,
            ClusterContextPool contextPool,
            CancellationToken token)
        {
            _maintenance = maintenance;
            _nodeTag = server.NodeTag;
            _server = server;
            _engine = engine;
            _term = term;
            _contextPool = contextPool;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _observerLogger = new ObserverLogger(_nodeTag);

            var config = server.Configuration.Cluster;
            _supervisorSamplePeriod = config.SupervisorSamplePeriod.AsTimeSpan;
            _stabilizationTime = config.StabilizationTime.AsTimeSpan;
            _stabilizationTimeMs = (long)config.StabilizationTime.AsTimeSpan.TotalMilliseconds;

            var now = DateTime.UtcNow;
            _databaseTopologyUpdater = new DatabaseTopologyUpdater(server, engine, config, clusterObserverStartTime: now, _observerLogger);
            _orchestratorTopologyUpdater = new OrchestratorTopologyUpdater(server, engine, config, clusterObserverStartTime: now, _observerLogger);

            _observe = PoolOfThreads.GlobalRavenThreadPool.LongRunning(_ =>
            {
                try
                {
                    Run(_cts.Token);
                }
                catch
                {
                    // nothing we can do here
                }
            }, null, $"Cluster observer for term {_term}");
        }

        public bool Suspended = false; // don't really care about concurrency here
        private long _iteration;
        private readonly long _term;
        private long _lastIndexCleanupTimeInTicks;
        internal long _lastTombstonesCleanupTimeInTicks;
        internal long _lastExpiredCompareExchangeCleanupTimeInTicks;
        private bool _hasMoreTombstones = false;

        public (ClusterObserverLogEntry[] List, long Iteration) ReadDecisionsForDatabase()
        {
            return (_observerLogger.DecisionsLog.ToArray(), _iteration);
        }

        public void Run(CancellationToken token)
        {
            // we give some time to populate the stats.
            if (token.WaitHandle.WaitOne(_stabilizationTime))
                return;

            var prevStats = _maintenance.GetStats();

            // wait before collecting the stats again.
            if (token.WaitHandle.WaitOne(_supervisorSamplePeriod))
                return;

            while (_term == _engine.CurrentTerm && token.IsCancellationRequested == false)
            {
                try
                {
                    if (Suspended == false)
                    {
                        _iteration++;
                        var newStats = _maintenance.GetStats();

                        // ReSharper disable once MethodSupportsCancellation
                        // we explicitly not passing a token here, since it will throw operation cancelled,
                        // but the original task might continue to run (with an open tx)

                        AnalyzeLatestStats(newStats, prevStats).Wait();
                        prevStats = newStats;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    Debug.Assert(e.InnerException is not KeyNotFoundException,
                        $"Got a '{nameof(KeyNotFoundException)}' while analyzing maintenance stats on node {_nodeTag} : {e}");

                    _observerLogger.Log($"An error occurred while analyzing maintenance stats on node {_nodeTag}.", _iteration, e);
                }
                finally
                {
                    token.WaitHandle.WaitOne(_supervisorSamplePeriod);
                }
            }
        }

        private async Task AnalyzeLatestStats(
            Dictionary<string, ClusterNodeStatusReport> newStats,
            Dictionary<string, ClusterNodeStatusReport> prevStats)
        {
            var currentLeader = _engine.CurrentLeader;
            if (currentLeader == null)
                return;

            var updateCommands = new List<(UpdateTopologyCommand Update, string Reason)>();
            var cleanUnusedAutoIndexesCommands = new List<(UpdateDatabaseCommand Update, string Reason)>();
            var cleanCompareExchangeTombstonesCommands = new List<CleanCompareExchangeTombstonesCommand>();

            Dictionary<string, long> cleanUpState = null;
            List<DeleteDatabaseCommand> deletions = null;
            List<DestinationMigrationConfirmCommand> confirmCommands = null;
            List<string> databases;

            using (_contextPool.AllocateOperationContext(out ClusterOperationContext context))
            using (context.OpenReadTransaction())
            {
                databases = _engine.StateMachine.GetDatabaseNames(context).ToList();
            }

            var now = Time.GetUtcNow();
            var cleanupIndexes = now.Ticks - _lastIndexCleanupTimeInTicks >= _server.Configuration.Indexing.CleanupInterval.AsTimeSpan.Ticks;
            var cleanupTombstones = now.Ticks - _lastTombstonesCleanupTimeInTicks >= _server.Configuration.Cluster.CompareExchangeTombstonesCleanupInterval.AsTimeSpan.Ticks;
            var cleanupExpiredCompareExchange = now.Ticks - _lastExpiredCompareExchangeCleanupTimeInTicks >= _server.Configuration.Cluster.CompareExchangeExpiredCleanupInterval.AsTimeSpan.Ticks;

            foreach (var database in databases)
            {
                using (_contextPool.AllocateOperationContext(out ClusterOperationContext context))
                using (context.OpenReadTransaction())
                {
                    var clusterTopology = _server.GetClusterTopology(context);

                    _cts.Token.ThrowIfCancellationRequested();

                    using (var rawRecord = _engine.StateMachine.ReadRawDatabaseRecord(context, database, out long etag))
                    {
                        if (rawRecord == null)
                        {
                            _observerLogger.Log($"Can't analyze the stats of database the {database}, because the database record is null.", iteration: _iteration, database: database);
                            continue;
                        }

                        if (rawRecord.IsSharded)
                        {
                            var databaseName = rawRecord.DatabaseName;
                            var topology = rawRecord.Sharding.Orchestrator.Topology;
                            var state = new DatabaseObservationState
                            {
                                Name = databaseName,
                                DatabaseTopology = topology,
                                ClusterTopology = clusterTopology,
                                Current = newStats,
                                Previous = prevStats,
                                RawDatabase = rawRecord,
                                LastIndexModification = etag,
                                ObserverIteration = _iteration
                            };

                            if (SkipAnalyzingDatabaseGroup(state, currentLeader, now))
                                continue;
                            
                            List<DeleteDatabaseCommand> unneededDeletions = null; // database deletions are irrelevant in orchestrator topology changes
                            var updateReason = _orchestratorTopologyUpdater.Update(context, state, ref unneededDeletions);
                            
                            if (updateReason != null)
                            {
                                _observerLogger.AddToDecisionLog(databaseName, updateReason, _iteration);

                                var cmd = new UpdateTopologyCommand(databaseName, now, RaftIdGenerator.NewId())
                                {
                                    Topology = topology,
                                    RaftCommandIndex = etag,
                                };
                                
                                updateCommands.Add((cmd, updateReason));
                            }

                            UpdateReshardingStatus(context, rawRecord, newStats, ref confirmCommands);
                        }

                        var mergedState = new MergedDatabaseObservationState(rawRecord);

                        foreach (var topology in rawRecord.Topologies)
                        {
                            var state = new DatabaseObservationState
                            {
                                Name = topology.Name,
                                DatabaseTopology = topology.Topology,
                                ClusterTopology = clusterTopology,
                                Current = newStats,
                                Previous = prevStats,
                                RawDatabase = rawRecord,
                                LastIndexModification = etag,
                                ObserverIteration = _iteration
                            };

                            mergedState.AddState(state);

                            if (SkipAnalyzingDatabaseGroup(state, currentLeader, now))
                                continue;

                            var updateReason =  _databaseTopologyUpdater.Update(context, state, ref deletions);
                            if (updateReason != null)
                            {
                                _observerLogger.AddToDecisionLog(state.Name, updateReason, state.ObserverIteration);

                                var cmd = new UpdateTopologyCommand(state.Name, now, RaftIdGenerator.NewId())
                                {
                                    Topology = state.DatabaseTopology,
                                    RaftCommandIndex = state.LastIndexModification,
                                };

                                updateCommands.Add((cmd, updateReason));
                            }
                        }

                        var cleanUp = mergedState.States.Min(s => CleanUpDatabaseValues(s.Value) ?? -1);
                        if (cleanUp > 0)
                        {
                            cleanUpState ??= new Dictionary<string, long>();
                            cleanUpState.Add(database, cleanUp);
                        }

                        if (cleanupIndexes)
                        {
                            foreach (var shardToState in mergedState.States)
                            {
                                var cleanupCommandsForDatabase = GetUnusedAutoIndexes(shardToState.Value);
                                cleanUnusedAutoIndexesCommands.AddRange(cleanupCommandsForDatabase);
                            }
                        }

                        if (cleanupTombstones)
                        {
                            foreach (var shardToState in mergedState.States)
                            {
                                var cmd = GetCompareExchangeTombstonesToCleanup(shardToState.Value.Name, shardToState.Value, context, out var cleanupState);
                                switch (cleanupState)
                                {
                                    case CompareExchangeTombstonesCleanupState.InvalidDatabaseObservationState:
                                        _hasMoreTombstones = true;
                                        break;
                                    case CompareExchangeTombstonesCleanupState.HasMoreTombstones:
                                        Debug.Assert(cmd != null);
                                        cleanCompareExchangeTombstonesCommands.Add(cmd);
                                        break;
                                    case CompareExchangeTombstonesCleanupState.InvalidPeriodicBackupStatus:
                                    case CompareExchangeTombstonesCleanupState.NoMoreTombstones:
                                        break;

                                    default:
                                        throw new NotSupportedException($"Not supported state: '{cleanupState}'.");
                                }
                            }
                        }
                    }
                }
            }

            if (cleanupIndexes)
            {
                foreach (var (cmd, updateReason) in cleanUnusedAutoIndexesCommands)
                {
                    await _engine.PutAsync(cmd);
                    _observerLogger.AddToDecisionLog(cmd.DatabaseName, updateReason, _iteration);
                }

                _lastIndexCleanupTimeInTicks = now.Ticks;
            }

            if (cleanupTombstones)
            {
                foreach (var cmd in cleanCompareExchangeTombstonesCommands)
                {
                    var result = await _server.SendToLeaderAsync(cmd);
                    await _server.Cluster.WaitForIndexNotification(result.Index);
                    var hasMore = (bool)result.Result;

                    _hasMoreTombstones |= hasMore;
                }

                if (_hasMoreTombstones == false)
                    _lastTombstonesCleanupTimeInTicks = now.Ticks;
            }

            if (cleanupExpiredCompareExchange)
            {
                if (await RemoveExpiredCompareExchange(now.Ticks) == false)
                    _lastExpiredCompareExchangeCleanupTimeInTicks = now.Ticks;
            }

            foreach (var command in updateCommands)
            {
                try
                {
                    await UpdateTopology(command.Update);
                    var alert = AlertRaised.Create(
                        command.Update.DatabaseName,
                        $"Topology of database '{command.Update.DatabaseName}' was changed",
                        command.Reason,
                        AlertType.DatabaseTopologyWarning,
                        NotificationSeverity.Warning
                    );
                    NotificationCenter.Add(alert);
                }
                catch (Exception e) when (e.ExtractSingleInnerException() is ConcurrencyException or RachisConcurrencyException)
                {
                    // this is sort of expected, if the database was
                    // modified by someone else, we'll avoid changing
                    // it and run the logic again on the next round
                    _observerLogger.AddToDecisionLog(command.Update.DatabaseName,
                        $"Topology of database '{command.Update.DatabaseName}' was not changed, reason: {nameof(ConcurrencyException)}", _iteration);
                }
            }

            if (deletions != null)
            {
                foreach (var command in deletions)
                {
                    _observerLogger.AddToDecisionLog(command.DatabaseName,
                        $"We reached the replication factor on '{command.DatabaseName}', so we try to remove promotables/rehabs from: {string.Join(", ", command.FromNodes)}", _iteration);

                    await Delete(command);
                }
            }

            if (cleanUpState != null)
            {
                var guid = "cleanup/" + GetCommandId(cleanUpState);
                if (_engine.ContainsCommandId(guid) == false)
                {
                    foreach (var kvp in cleanUpState)
                    {
                        _observerLogger.AddToDecisionLog(kvp.Key, $"Should clean up values up to raft index {kvp.Value}.", _iteration);
                    }

                    var cmd = new CleanUpClusterStateCommand(guid) { ClusterTransactionsCleanup = cleanUpState };

                    if (_engine.LeaderTag != _server.NodeTag)
                    {
                        throw new NotLeadingException("This node is no longer the leader, so abort the cleaning.");
                    }

                    await _engine.PutAsync(cmd);
                }
            }

            if (confirmCommands != null)
            {
                foreach (var confirmCommand in confirmCommands)
                {
                    await _engine.PutAsync(confirmCommand);
                }
            }
        }

        private void UpdateReshardingStatus(ClusterOperationContext context, RawDatabaseRecord rawRecord, Dictionary<string, ClusterNodeStatusReport> newStats, ref List<DestinationMigrationConfirmCommand> confirmCommands)
        {
            if (_server.Server.ServerStore.Sharding.ManualMigration)
                return;

            var databaseName = rawRecord.DatabaseName;
            var sharding = rawRecord.Sharding;
            var currentMigration = sharding.BucketMigrations.SingleOrDefault(pair => pair.Value.Status == MigrationStatus.Moved).Value;
            if (currentMigration == null)
                return;

            var destination = ShardHelper.ToShardName(databaseName, currentMigration.DestinationShard);
            foreach (var node in newStats)
            {
                var tag = node.Key;
                var nodeReport = node.Value;

                if (nodeReport.Report.TryGetValue(destination, out var destinationReport))
                {
                    if (destinationReport.ReportPerBucket.TryGetValue(currentMigration.Bucket, out var bucketReport))
                    {
                        var lastFromSrc = context.GetChangeVector(currentMigration.LastSourceChangeVector);
                        var currentFromDest = context.GetChangeVector(bucketReport.LastChangeVector);
                        var status = ChangeVectorUtils.GetConflictStatus(lastFromSrc.Version, currentFromDest.Version);
                        if (status == ConflictStatus.AlreadyMerged)
                        {
                            confirmCommands ??= new List<DestinationMigrationConfirmCommand>();
                            confirmCommands.Add(new DestinationMigrationConfirmCommand(currentMigration.Bucket,
                                currentMigration.MigrationIndex, tag, databaseName, $"Confirm-{currentMigration.Bucket}@{currentMigration.MigrationIndex}/{tag}"));
                            continue;
                        }
                    }

                    if (currentMigration.LastSourceChangeVector == null)
                    {
                        // moving empty bucket
                        confirmCommands ??= new List<DestinationMigrationConfirmCommand>();
                        confirmCommands.Add(new DestinationMigrationConfirmCommand(currentMigration.Bucket,
                            currentMigration.MigrationIndex, tag, databaseName, $"Confirm-{currentMigration.Bucket}@{currentMigration.MigrationIndex}/{tag}"));
                    }
                }
            }
        }

        private bool SkipAnalyzingDatabaseGroup(DatabaseObservationState state, Leader currentLeader, DateTime now)
        {
            var databaseTopology = state.DatabaseTopology;
            var databaseName = state.Name;

            if (databaseTopology == null)
            {
                _observerLogger.Log($"Can't analyze the stats of database the {databaseName}, because the database topology is null.", _iteration, database: databaseName);
                return true;
            }

            if (databaseTopology.Count == 0)
            {
                // database being deleted
                _observerLogger.Log($"Skip analyze the stats of database the {databaseName}, because it being deleted", _iteration, database: databaseName);
                return true;
            }

            var topologyStamp = databaseTopology.Stamp;
            var graceIfLeaderChanged = _term > topologyStamp.Term && currentLeader.LeaderShipDuration < _stabilizationTimeMs;
            var letStatsBecomeStable = _term == topologyStamp.Term &&
                                       ((now - (databaseTopology.NodesModifiedAt ?? DateTime.MinValue)).TotalMilliseconds < _stabilizationTimeMs);

            if (graceIfLeaderChanged || letStatsBecomeStable)
            {
                _observerLogger.Log($"We give more time for the '{databaseName}' stats to become stable, so we skip analyzing it for now.", _iteration, database: databaseName);
                return true;
            }

            if (state.ReadDatabaseDisabled())
                return true;

            return false;
        }

        private static string GetCommandId(Dictionary<string, long> dic)
        {
            if (dic == null)
                return Guid.Empty.ToString();

            var hash = 0UL;
            foreach (var kvp in dic)
            {
                hash = Hashing.XXHash64.CalculateRaw(kvp.Key) ^ (ulong)kvp.Value ^ hash;
            }

            return hash.ToString("X");
        }

        internal List<(UpdateDatabaseCommand Update, string Reason)> GetUnusedAutoIndexes(DatabaseObservationState databaseState)
        {
            const string autoIndexPrefix = "Auto/";
            var cleanupCommands = new List<(UpdateDatabaseCommand Update, string Reason)>();

            if (AllDatabaseNodesHasReport(databaseState) == false)
                return cleanupCommands;

            var indexes = new Dictionary<string, TimeSpan>();

            var lowestDatabaseUpTime = TimeSpan.MaxValue;
            var newestIndexQueryTime = TimeSpan.MaxValue;

            foreach (var node in databaseState.DatabaseTopology.AllNodes)
            {
                if (databaseState.Current.TryGetValue(node, out var nodeReport) == false)
                    return cleanupCommands;

                if (nodeReport.Report.TryGetValue(databaseState.Name, out var report) == false)
                    return cleanupCommands;

                if (report.UpTime.HasValue && lowestDatabaseUpTime > report.UpTime)
                    lowestDatabaseUpTime = report.UpTime.Value;

                foreach (var kvp in report.LastIndexStats)
                {
                    var lastQueried = kvp.Value.LastQueried;
                    if (lastQueried.HasValue == false)
                        continue;

                    if (newestIndexQueryTime > lastQueried.Value)
                        newestIndexQueryTime = lastQueried.Value;

                    var indexName = kvp.Key;
                    if (indexName.StartsWith(autoIndexPrefix, StringComparison.OrdinalIgnoreCase) == false)
                        continue;

                    if (indexes.TryGetValue(indexName, out var lq) == false || lq > lastQueried)
                    {
                        indexes[indexName] = lastQueried.Value;
                    }
                }
            }

            if (indexes.Count == 0)
                return cleanupCommands;

            var settings = databaseState.ReadSettings();
            var timeToWaitBeforeMarkingAutoIndexAsIdle = (TimeSetting)RavenConfiguration.GetValue(x => x.Indexing.TimeToWaitBeforeMarkingAutoIndexAsIdle, _server.Configuration, settings);
            var timeToWaitBeforeDeletingAutoIndexMarkedAsIdle = (TimeSetting)RavenConfiguration.GetValue(x => x.Indexing.TimeToWaitBeforeDeletingAutoIndexMarkedAsIdle, _server.Configuration, settings);

            foreach (var kvp in indexes)
            {
                TimeSpan difference;
                if (lowestDatabaseUpTime > kvp.Value)
                    difference = kvp.Value;
                else
                {
                    difference = kvp.Value - newestIndexQueryTime;
                    if (difference == TimeSpan.Zero && lowestDatabaseUpTime > kvp.Value)
                        difference = kvp.Value;
                }

                var state = IndexState.Normal;
                if (databaseState.TryGetAutoIndex(kvp.Key, out var definition) && definition.State.HasValue)
                    state = definition.State.Value;

                if (state == IndexState.Idle && difference >= timeToWaitBeforeDeletingAutoIndexMarkedAsIdle.AsTimeSpan)
                {
                    var deleteIndexCommand = new DeleteIndexCommand(kvp.Key, databaseState.Name, RaftIdGenerator.NewId());
                    var updateReason = $"Deleting idle auto-index '{kvp.Key}' because last query time value is '{difference}' and threshold is set to '{timeToWaitBeforeDeletingAutoIndexMarkedAsIdle.AsTimeSpan}'.";

                    cleanupCommands.Add((deleteIndexCommand, updateReason));
                    continue;
                }

                if (state == IndexState.Normal && difference >= timeToWaitBeforeMarkingAutoIndexAsIdle.AsTimeSpan)
                {
                    var setIndexStateCommand = new SetIndexStateCommand(kvp.Key, IndexState.Idle, databaseState.Name, RaftIdGenerator.NewId());
                    var updateReason = $"Marking auto-index '{kvp.Key}' as idle because last query time value is '{difference}' and threshold is set to '{timeToWaitBeforeMarkingAutoIndexAsIdle.AsTimeSpan}'.";

                    cleanupCommands.Add((setIndexStateCommand, updateReason));
                    continue;
                }

                if (state == IndexState.Idle && difference < timeToWaitBeforeMarkingAutoIndexAsIdle.AsTimeSpan)
                {
                    var setIndexStateCommand = new SetIndexStateCommand(kvp.Key, IndexState.Normal, databaseState.Name, Guid.NewGuid().ToString());
                    var updateReason = $"Marking idle auto-index '{kvp.Key}' as normal because last query time value is '{difference}' and threshold is set to '{timeToWaitBeforeMarkingAutoIndexAsIdle.AsTimeSpan}'.";

                    cleanupCommands.Add((setIndexStateCommand, updateReason));
                }
            }

            return cleanupCommands;
        }

        internal CleanCompareExchangeTombstonesCommand GetCompareExchangeTombstonesToCleanup(string databaseName, DatabaseObservationState state, ClusterOperationContext context, out CompareExchangeTombstonesCleanupState cleanupState)
        {
            const int amountToDelete = 8192;

            if (_server.Cluster.HasCompareExchangeTombstones(context, databaseName) == false)
            {
                cleanupState = CompareExchangeTombstonesCleanupState.NoMoreTombstones;
                return null;
            }

            cleanupState = GetMaxCompareExchangeTombstonesEtagToDelete(context, databaseName, state, out long maxEtag);

            return cleanupState == CompareExchangeTombstonesCleanupState.HasMoreTombstones
                ? new CleanCompareExchangeTombstonesCommand(databaseName, maxEtag, amountToDelete, RaftIdGenerator.NewId())
                : null;
        }

        public enum CompareExchangeTombstonesCleanupState
        {
            HasMoreTombstones,
            InvalidDatabaseObservationState,
            InvalidPeriodicBackupStatus,
            NoMoreTombstones
        }

        private CompareExchangeTombstonesCleanupState GetMaxCompareExchangeTombstonesEtagToDelete<TRavenTransaction>(TransactionOperationContext<TRavenTransaction> context, string databaseName, DatabaseObservationState state, out long maxEtag) where TRavenTransaction : RavenTransaction
        {
            List<long> periodicBackupTaskIds;
            maxEtag = long.MaxValue;

            if (state?.RawDatabase != null)
            {
                periodicBackupTaskIds = state.RawDatabase.PeriodicBackupsTaskIds;
            }
            else
            {
                using (var rawRecord = _server.Cluster.ReadRawDatabaseRecord(context, databaseName))
                    periodicBackupTaskIds = rawRecord.PeriodicBackupsTaskIds;
            }

            if (periodicBackupTaskIds != null && periodicBackupTaskIds.Count > 0)
            {
                foreach (var taskId in periodicBackupTaskIds)
                {
                    var singleBackupStatus = _server.Cluster.Read(context, PeriodicBackupStatus.GenerateItemName(databaseName, taskId));
                    if (singleBackupStatus == null)
                        continue;

                    if (singleBackupStatus.TryGet(nameof(PeriodicBackupStatus.LastFullBackupInternal), out DateTime? lastFullBackupInternal) == false || lastFullBackupInternal == null)
                    {
                        // never backed up yet
                        if (singleBackupStatus.TryGet(nameof(PeriodicBackupStatus.LastIncrementalBackupInternal), out DateTime? lastIncrementalBackupInternal) == false || lastIncrementalBackupInternal == null)
                            continue;
                    }

                    if (singleBackupStatus.TryGet(nameof(PeriodicBackupStatus.LastRaftIndex), out BlittableJsonReaderObject lastRaftIndexBlittable) == false ||
                        lastRaftIndexBlittable == null)
                    {
                        if (singleBackupStatus.TryGet(nameof(PeriodicBackupStatus.Error), out BlittableJsonReaderObject error) == false || error != null)
                        {
                            // backup errored on first run (lastRaftIndex == null) => cannot remove ANY tombstones
                            return CompareExchangeTombstonesCleanupState.InvalidPeriodicBackupStatus;
                        }

                        continue;
                    }

                    if (lastRaftIndexBlittable.TryGet(nameof(PeriodicBackupStatus.LastEtag), out long? lastRaftIndex) == false || lastRaftIndex == null)
                    {
                        continue;
                    }

                    if (lastRaftIndex < maxEtag)
                        maxEtag = lastRaftIndex.Value;

                    if (maxEtag == 0)
                        return CompareExchangeTombstonesCleanupState.NoMoreTombstones;
                }
            }

            if (state != null)
            {
                if (state.DatabaseTopology.Count != state.Current.Count) // we have a state change, do not remove anything
                    return CompareExchangeTombstonesCleanupState.InvalidDatabaseObservationState;

                foreach (var node in state.DatabaseTopology.AllNodes)
                {
                    if (state.Current.TryGetValue(node, out var nodeReport) == false)
                        continue;

                    if (nodeReport.Report.TryGetValue(state.Name, out var report) == false)
                        continue;

                    foreach (var kvp in report.LastIndexStats)
                    {
                        var lastIndexedCompareExchangeReferenceTombstoneEtag = kvp.Value.LastIndexedCompareExchangeReferenceTombstoneEtag;
                        if (lastIndexedCompareExchangeReferenceTombstoneEtag == null)
                            continue;

                        if (lastIndexedCompareExchangeReferenceTombstoneEtag < maxEtag)
                            maxEtag = lastIndexedCompareExchangeReferenceTombstoneEtag.Value;

                        if (maxEtag == 0)
                            return CompareExchangeTombstonesCleanupState.NoMoreTombstones;
                    }
                }
            }

            if (maxEtag == 0)
                return CompareExchangeTombstonesCleanupState.NoMoreTombstones;

            return CompareExchangeTombstonesCleanupState.HasMoreTombstones;
        }

        private async Task<bool> RemoveExpiredCompareExchange(long nowTicks)
        {
            const int batchSize = 1024;
            using (_contextPool.AllocateOperationContext(out ClusterOperationContext context))
            using (context.OpenReadTransaction())
            {
                if (CompareExchangeExpirationStorage.HasExpired(context, nowTicks) == false)
                    return false;
            }

            var result = await _server.SendToLeaderAsync(new DeleteExpiredCompareExchangeCommand(nowTicks, batchSize, RaftIdGenerator.NewId()));
            await _server.Cluster.WaitForIndexNotification(result.Index);
            return (bool)result.Result;
        }

        private long? CleanUpDatabaseValues(DatabaseObservationState state)
        {
            if (ClusterCommandsVersionManager.CurrentClusterMinimalVersion <
                ClusterCommandsVersionManager.ClusterCommandsVersions[nameof(CleanUpClusterStateCommand)])
            {
                return null;
            }

            if (AllDatabaseNodesHasReport(state) == false)
                return null;

            long commandCount = long.MaxValue;
            foreach (var node in state.DatabaseTopology.AllNodes)
            {
                if (state.Current.TryGetValue(node, out var nodeReport) == false)
                    return null;

                if (nodeReport.Report.TryGetValue(state.Name, out var report) == false)
                    return null;

                commandCount = Math.Min(commandCount, report.LastCompletedClusterTransaction);
            }

            if (commandCount <= state.ReadTruncatedClusterTransactionCommandsCount())
                return null;

            return commandCount;
        }

        private static bool AllDatabaseNodesHasReport(DatabaseObservationState state)
        {
            if (state == null)
                return false;

            if (state.DatabaseTopology.Count == 0)
                return false; // database is being deleted, so no need to cleanup values

            foreach (var node in state.DatabaseTopology.AllNodes)
            {
                if (state.Current.ContainsKey(node) == false)
                    return false;
            }

            return true;
        }
        
        private Task<(long Index, object Result)> UpdateTopology(UpdateTopologyCommand cmd)
        {
            if (_engine.LeaderTag != _server.NodeTag)
            {
                throw new NotLeadingException("This node is no longer the leader, so we abort updating the database databaseTopology");
            }

            return _engine.PutAsync(cmd);
        }

        private Task<(long Index, object Result)> Delete(DeleteDatabaseCommand cmd)
        {
            if (_engine.LeaderTag != _server.NodeTag)
            {
                throw new NotLeadingException("This node is no longer the leader, so we abort the deletion command");
            }
            return _engine.PutAsync(cmd);
        }

        public void Dispose()
        {
            _cts.Cancel();

            try
            {
                if (_observe.Join((int)TimeSpan.FromSeconds(30).TotalMilliseconds) == false)
                {
                    throw new ObjectDisposedException($"Cluster observer on node {_nodeTag} still running and can't be closed");
                }
            }
            finally
            {
                _cts.Dispose();
            }
        }

        internal class MergedDatabaseObservationState
        {
            public static MergedDatabaseObservationState Empty = new MergedDatabaseObservationState();
            private readonly bool _isShardedState;

            public MergedDatabaseObservationState(RawDatabaseRecord record)
            {
                RawDatabase = record;
                _isShardedState = RawDatabase.IsSharded;

                var length = _isShardedState ? RawDatabase.Sharding.Shards.Count : 1;
                States = new Dictionary<int, DatabaseObservationState>(length);
            }

            public MergedDatabaseObservationState(RawDatabaseRecord record, DatabaseObservationState state) : this(record)
            {
                AddState(state);
            }

            private MergedDatabaseObservationState()
            {
                States = new Dictionary<int, DatabaseObservationState>(1);
            }

            public void AddState(DatabaseObservationState state)
            {
                if (ShardHelper.TryGetShardNumberFromDatabaseName(state.Name, out var shardNumber) == false)
                {
                    // handle not sharded database
                    if (_isShardedState)
                        throw new InvalidOperationException($"The database {state.Name} isn't sharded, but was initialized as one.");

                    States[0] = state;
                    return;
                }

                if (_isShardedState == false)
                    throw new InvalidOperationException($"The database {state.Name} is sharded (shard: {shardNumber}), but was initialized as a regular one.");

                States[shardNumber] = state;
            }

            public readonly Dictionary<int, DatabaseObservationState> States;
            public readonly RawDatabaseRecord RawDatabase;
        }

        internal class DatabaseObservationState
        {
            public string Name;
            public DatabaseTopology DatabaseTopology;
            public Dictionary<string, ClusterNodeStatusReport> Current;
            public Dictionary<string, ClusterNodeStatusReport> Previous;
            public ClusterTopology ClusterTopology;

            public RawDatabaseRecord RawDatabase;
            public long LastIndexModification;
            public long ObserverIteration;

            public long ReadTruncatedClusterTransactionCommandsCount()
            {
                RawDatabase.Raw.TryGet(nameof(DatabaseRecord.TruncatedClusterTransactionCommandsCount), out long count);
                return count;
            }

            public bool TryGetAutoIndex(string name, out AutoIndexDefinition definition)
            {
                BlittableJsonReaderObject autoDefinition = null;
                definition = null;
                RawDatabase.Raw.TryGet(nameof(DatabaseRecord.AutoIndexes), out BlittableJsonReaderObject autoIndexes);
                if (autoIndexes?.TryGet(name, out autoDefinition) == false)
                    return false;

                definition = JsonDeserializationServer.AutoIndexDefinition(autoDefinition);
                return true;
            }

            public Dictionary<string, DeletionInProgressStatus> ReadDeletionInProgress()
            {
                return RawDatabase.DeletionInProgress;
            }

            public bool ReadDatabaseDisabled()
            {
                return RawDatabase.IsDisabled;
            }

            public bool ReadRestoringInProgress()
            {
                return RawDatabase.DatabaseState == DatabaseStateStatus.RestoreInProgress;
            }

            public Dictionary<string, string> ReadSettings()
            {
                return RawDatabase.Settings;
            }

            public DatabaseStatusReport GetCurrentDatabaseReport(string node)
            {
                if (Current.TryGetValue(node, out var report) == false)
                    return null;

                if (report.Report.TryGetValue(Name, out var databaseReport) == false)
                    return null;

                return databaseReport;
            }

            public DatabaseStatusReport GetPreviousDatabaseReport(string node)
            {
                if (Previous.TryGetValue(node, out var report) == false)
                    return null;

                if (report.Report.TryGetValue(Name, out var databaseReport) == false)
                    return null;

                return databaseReport;
            }

            public static implicit operator MergedDatabaseObservationState(DatabaseObservationState state)
            {
                return new MergedDatabaseObservationState(state.RawDatabase, state);
            }
        }
    }
}
