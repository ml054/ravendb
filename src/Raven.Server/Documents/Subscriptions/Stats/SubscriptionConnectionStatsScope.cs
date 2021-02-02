// -----------------------------------------------------------------------
//  <copyright file="SubscriptionStatsScope.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using Raven.Server.Documents.ETL.Stats;
using Raven.Server.Utils.Stats;

namespace Raven.Server.Documents.Subscriptions.Stats
{
    public class SubscriptionConnectionStatsScope : StatsScope<SubscriptionConnectionRunStats, SubscriptionConnectionStatsScope>
    {
        public EtlStatsScope Mimic; //TODO: 
        
        public SubscriptionConnectionStatsScope(SubscriptionConnectionRunStats stats, bool start = true) : base(stats, start)
        {
            //TODO: 
        }

        protected override SubscriptionConnectionStatsScope OpenNewScope(SubscriptionConnectionRunStats stats, bool start)
        {
            throw new System.NotImplementedException();
        }
    }
}
