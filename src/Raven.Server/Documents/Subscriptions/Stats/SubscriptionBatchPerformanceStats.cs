// -----------------------------------------------------------------------
//  <copyright file="SubscriptionPeformanceStats.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Raven.Client.Documents.Indexes;
using Raven.Server.Documents.ETL.Stats;

namespace Raven.Server.Documents.Subscriptions
{
    public class SubscriptionBatchPerformanceStats
    {
        public EtlPerformanceStats Mimic { get; set; } //tODO: remove me!
        public IndexingPerformanceStats Mimic2; //TODO: 

        public int Id { get; set; }

        public DateTime Started { get; set; }

        public DateTime? Completed { get; set; }

        public double DurationInMs { get; }
        
    }
}
