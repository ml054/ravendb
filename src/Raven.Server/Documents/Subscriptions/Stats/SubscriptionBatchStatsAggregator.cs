// -----------------------------------------------------------------------
//  <copyright file="SubscriptionStatsAggregator.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using Raven.Server.Documents.ETL.Stats;
using Raven.Server.Utils.Stats;

namespace Raven.Server.Documents.Subscriptions.Stats
{
    public class SubscriptionBatchStatsAggregator : StatsAggregator<SubscriptionBatchRunStats, SubscriptionBatchStatsScope>
    {
        public EtlStatsAggregator MImic; //TODO
        
        public SubscriptionBatchStatsAggregator(int id, SubscriptionBatchStatsAggregator lastStats) : base(id, lastStats)
        {
            throw new System.NotImplementedException();
        }

        public SubscriptionBatchPerformanceStats ToSubscriptionPerformanceLiveStatsWithDetails()
        {
            throw new System.NotImplementedException();
        }

        public override SubscriptionBatchStatsScope CreateScope()
        {
            Debug.Assert(Scope == null);

            return Scope = new SubscriptionBatchStatsScope(Stats);
        }
    }
}
