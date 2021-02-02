// -----------------------------------------------------------------------
//  <copyright file="SubscriptionTaskPerformanceStats.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using Raven.Server.Documents.ETL.Stats;
using Sparrow.Json;

namespace Raven.Server.Documents.Subscriptions
{
    public class SubscriptionTaskBatchPerformanceStats : ISubscriptionPerformanceStats
    {
        public string TaskName { get; set; }
        
        //TODO: task id or subscription name ? 
        
        public SubscriptionBatchPerformanceStats[] Performance { get; set; }


        public void Write(JsonOperationContext context, AbstractBlittableJsonTextWriter writer)
        {
            //TODO: 
        }
    }
}
