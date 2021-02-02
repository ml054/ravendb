// -----------------------------------------------------------------------
//  <copyright file="LiveSubscriptionPerformanceCollector.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Documents.Subscriptions;
using Raven.Server.Documents.Subscriptions.Stats;
using Raven.Server.Documents.TcpHandlers;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils.Stats;
using Sparrow.Json;

namespace Raven.Server.Documents.Subscriptions
{
    public class LiveSubscriptionPerformanceCollector : DatabaseAwareLivePerformanceCollector<ISubscriptionPerformanceStats>
    {
        // TODO holds reference to current connection (so we can fetch latest - in progress stats) + ref to list (successful or failed) connections that have ended  
        private readonly ConcurrentDictionary<string, SubscriptionAndPerformanceConnectionStatsList> _perSubscriptionConnectionStats
            = new ConcurrentDictionary<string, SubscriptionAndPerformanceConnectionStatsList>();
        
        // TODO: allows to access current batch + completed batches
        private readonly ConcurrentDictionary<string, SubscriptionAndPerformanceBatchStatsList> _perSubscriptionBatchStats 
            = new ConcurrentDictionary<string, SubscriptionAndPerformanceBatchStatsList>();
        
        public LiveSubscriptionPerformanceCollector(DocumentDatabase database) : base(database)
        {
            var recentStats = PrepareInitialPerformanceStats().ToList();
            if (recentStats.Count > 0)
            {
                Stats.Enqueue(recentStats);
            }
            
            Start();
        }

        protected IEnumerable<ISubscriptionPerformanceStats> PrepareInitialPerformanceStats()
        {
            using (Database.ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (context.OpenReadTransaction())
            {
                foreach (var subscription in 
                    Database.SubscriptionStorage.GetAllSubscriptions(context, false, 0, int.MaxValue))
                {
                    var subscriptionName = subscription.SubscriptionName;
                    
                    _perSubscriptionConnectionStats[subscriptionName] = new SubscriptionAndPerformanceConnectionStatsList(subscription);
                    _perSubscriptionBatchStats[subscriptionName] = new SubscriptionAndPerformanceBatchStatsList(subscription);
                    
                    //TODO: please remember to update connection when established!
                }

                foreach (var kvp in _perSubscriptionConnectionStats)
                {
                    var connectionState = Database.SubscriptionStorage.GetSubscriptionConnection(context, kvp.Value.Handler.SubscriptionName);
                    var currentConnection = connectionState.Connection;

                    var connectionResults = new List<SubscriptionConnectionStatsAggregator>();
                    var batchResult = new List<SubscriptionBatchStatsAggregator>();
                    
                    if (currentConnection != null)
                    {
                        connectionResults.Add(currentConnection.PerfStats);
                        batchResult.AddRange(currentConnection._lastBatchesStats);
                    }
                    
                    foreach (SubscriptionConnection connection in connectionState.RecentConnections)
                    {
                        connectionResults.Add(connection.PerfStats);
                        batchResult.AddRange(connection._lastBatchesStats);
                    }
                    
                    foreach (SubscriptionConnection connection in connectionState.RecentRejectedConnections)
                    {
                        connectionResults.Add(connection.PerfStats);
                    }
                    
                    connectionResults.AddRange(connectionState.RecentConnections.Select(x => x.PerfStats));
                    connectionResults.AddRange(connectionState.RecentRejectedConnections.Select(x => x.PerfStats));
                    
                    // now map SubscriptionConnectionStatsAggregator -> ISubscriptionPerformanceStats
                    
                    
                    
                    
                    


                    //connectionState.RecentConnections
                    //connectionState.RecentRejectedConnections

                    // we have list of SubscriptionConnection
                    // get historial connection stats!
                    // get historial subscription batches stats!

                    //TODO: var connection = Database.SubscriptionStorage.GetSubscriptionConnection(context, subscriptionName);
                    // grap existing (historical) connection performacne stats subscriptionAndPerformanceConnectionStatsList.Value.Handler.GetConnectionPerformanceStats()
                    // grap existing (historical) batch performacen subscriptionAndPerformanceConnectionStatsList.Value.Handler.GetBatchPerformanceStats()
                }

                //TODO: iterate through dict and get initial data
            }
        }

        protected override async Task StartCollectingStats()
        {
            // TODO: hook into batch completed 
            // TODO  hook into connection events
            // TODO: hook into subscription created/deleted
            try
            {
                List<ISubscriptionPerformanceStats> stats = new List<ISubscriptionPerformanceStats>();
                foreach (var x in Client.Extensions.EnumerableExtension.ForceEnumerateInThreadSafeManner(_perSubscriptionBatchStats))
                {
                    var handler = x.Value.Handler;
                    
                    stats.Add(new SubscriptionTaskBatchPerformanceStats
                    {
                        TaskName = handler.State.GetTaskName(),
                        Performance = handler.ConnectionState.GetBatchPerformanceStats()
                    });
                }
                
                Stats.Enqueue(stats);

                await RunInLoop();
            }
            finally
            {
                //TODO: remove hooks
            }
        }

        protected override List<ISubscriptionPerformanceStats> PreparePerformanceStats()
        {
            var preparedStats = new List<ISubscriptionPerformanceStats>(_perSubscriptionBatchStats.Count);

            foreach (var keyValue in _perSubscriptionBatchStats)
            {
                // This is done this way instead of using
                // _perSubscriptionStats.Values because .Values locks the entire
                // dictionary.
                var subscriptionAndPerformanceStatsList = keyValue.Value;
                var subscriptionAndStats = subscriptionAndPerformanceStatsList.Handler;
                var performance = subscriptionAndPerformanceStatsList.Performance;
                
                var itemsToSend = new List<SubscriptionBatchStatsAggregator>(performance.Count);

                while (performance.TryTake(out SubscriptionBatchStatsAggregator stat))
                    itemsToSend.Add(stat);
                
                /*
                // if index still exists let's fetch latest stats from live instance 
                var index = Database.IndexStore.GetIndex(subscriptionAndStats);

                var latestStats = index?.GetLatestIndexingStat();
                if (latestStats != null &&
                    latestStats.Completed == false && 
                    itemsToSend.Contains(latestStats) == false)
                    itemsToSend.Add(latestStats);
                    */

                if (itemsToSend.Count > 0)
                {
                    preparedStats.Add(new SubscriptionTaskBatchPerformanceStats
                    {
                        TaskName = subscriptionAndStats.State.GetTaskName(),
                        Performance = itemsToSend.Select(item => item.ToSubscriptionPerformanceLiveStatsWithDetails()).ToArray()
                    });
                }
            }
            return preparedStats;
        }

        protected override void WriteStats(List<ISubscriptionPerformanceStats> stats, AsyncBlittableJsonTextWriter writer, JsonOperationContext context)
        {
            Console.WriteLine(stats); //TODO: 
            writer.WriteStartObject(); //TODO: 
            writer.WriteEndObject(); //TODO
        }

        private class SubscriptionAndPerformanceBatchStatsList
            : HandlerAndPerformanceStatsList<SubscriptionState, SubscriptionBatchStatsAggregator>
        {
            public SubscriptionAndPerformanceBatchStatsList(SubscriptionState subscription) : base(subscription)
            {
                //TODO: task name/id ? 
            }
        }
        
        private class SubscriptionAndPerformanceConnectionStatsList
            : HandlerAndPerformanceStatsList<SubscriptionState, SubscriptionConnectionStatsAggregator>
        {
            public SubscriptionAndPerformanceConnectionStatsList(SubscriptionState subscription) : base(subscription)
            {
                //TODO: task name/id ? 
            }
        }
    }

   
    
    public interface ISubscriptionPerformanceStats
    {
        void Write(JsonOperationContext context, AbstractBlittableJsonTextWriter writer);
    }
}
