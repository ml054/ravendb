﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Server.Routing;
using Sparrow;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.Handlers
{
    public class PerformanceMetricsHandler : DatabaseRequestHandler
    {
        public class PerformanceMetricsResponse
        {
            public PerformanceMetricsResponse()
            {
                PerfMetrics = new List<PerformanceMetrics>();
            }

            public List<PerformanceMetrics> PerfMetrics { get; set; }

            public DynamicJsonValue ToJson()
            {
                return new DynamicJsonValue
                {
                    [nameof(PerfMetrics)] = new DynamicJsonArray(PerfMetrics.Select(x => x.ToJson()))
                };
            }
        }

        [RavenAction("/databases/*/debug/perf-metrics", "GET")]
        public Task IoMetrics()
        {
            JsonOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                var result = GetPerformanceMetricsResponse(Database);
                context.Write(writer, result.ToJson());
            }
            return Task.CompletedTask;
        }



        public static PerformanceMetricsResponse GetPerformanceMetricsResponse(DocumentDatabase documentDatabase)
        {
            var result = new PerformanceMetricsResponse();

            foreach (var metrics in documentDatabase.GetAllPerformanceMetrics())
            {
                result.PerfMetrics.Add(metrics.Buffer);
            }

            return result;
        }

    }
}
