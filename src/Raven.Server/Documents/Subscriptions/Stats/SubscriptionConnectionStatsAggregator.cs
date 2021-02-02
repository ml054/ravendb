// -----------------------------------------------------------------------
//  <copyright file="SubscriptionConnectionStatsAggregator.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using Raven.Server.Documents.ETL.Stats;
using Raven.Server.Utils.Stats;

namespace Raven.Server.Documents.Subscriptions.Stats
{
    public class SubscriptionConnectionStatsAggregator : StatsAggregator<SubscriptionConnectionRunStats, SubscriptionConnectionStatsScope>
    {
        public EtlStatsAggregator MImic; //TODO
        
        public SubscriptionConnectionStatsAggregator(int id, SubscriptionConnectionStatsAggregator lastStats) : base(id, lastStats)
        {
            throw new System.NotImplementedException();
        }

        public SubscriptionConnectionPerformanceStats ToSubscriptionPerformanceLiveStatsWithDetails()
        {
            throw new System.NotImplementedException();
        }

        public override SubscriptionConnectionStatsScope CreateScope()
        {
            throw new System.NotImplementedException();
        }
    }
}
