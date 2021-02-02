// -----------------------------------------------------------------------
//  <copyright file="SubscriptionStatsScope.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using Raven.Server.Documents.ETL.Stats;
using Raven.Server.Utils.Stats;

namespace Raven.Server.Documents.Subscriptions.Stats
{
    public class SubscriptionBatchStatsScope : StatsScope<SubscriptionBatchRunStats, SubscriptionBatchStatsScope>
    {
        public EtlStatsScope Mimic; //TODO: 
        
        public SubscriptionBatchStatsScope(SubscriptionBatchRunStats stats, bool start = true) : base(stats, start)
        {
            //TODO: 
        }

        protected override SubscriptionBatchStatsScope OpenNewScope(SubscriptionBatchRunStats stats, bool start)
        {
            throw new System.NotImplementedException();
        }
    }
}
