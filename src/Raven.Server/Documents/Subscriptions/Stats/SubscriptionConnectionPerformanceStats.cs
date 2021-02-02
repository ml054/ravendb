// -----------------------------------------------------------------------
//  <copyright file="SubscriptionConnectionPerformanceStats.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Raven.Server.Documents.Subscriptions.Stats
{
    public class SubscriptionConnectionPerformanceStats
    {
        public int Id { get; set; }

        public DateTime Started { get; set; }

        public DateTime? Completed { get; set; }
    }
}
