﻿using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Raven.Client.Documents.Exceptions.Indexes;
using Raven.Server.Json;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;
using Sparrow.Extensions;
using Sparrow.Json;

namespace Raven.Server.Documents.Handlers.Debugging
{
    public class QueriesDebugHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/debug/queries/kill", "GET")]
        public Task KillQuery()
        {
            var name = GetQueryStringValueAndAssertIfSingleAndNotEmpty("indexName");
            var id = GetLongQueryString("id");

            var index = Database.IndexStore.GetIndex(name);
            if (index == null)
                IndexDoesNotExistException.ThrowFor(name);

            var query = index.CurrentlyRunningQueries
                .FirstOrDefault(q => q.QueryId == id);

            if (query == null)
            {
                HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return Task.CompletedTask;
            }

            query.Token.Cancel();

            return NoContent();
        }

        [RavenAction("/databases/*/debug/queries/running", "GET")]
        public Task RunningQueries()
        {
            var indexes = Database
                .IndexStore
                .GetIndexes()
                .ToList();

            DocumentsOperationContext context;
            using (ContextPool.AllocateOperationContext(out context))
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                writer.WriteStartObject();
                foreach (var index in indexes)
                {
                    writer.WritePropertyName(index.Name);
                    writer.WriteStartArray();

                    var isFirstInternal = true;
                    foreach (var query in index.CurrentlyRunningQueries)
                    {
                        if (isFirstInternal == false)
                            writer.WriteComma();

                        isFirstInternal = false;

                        writer.WriteStartObject();

                        writer.WritePropertyName((nameof(query.Duration)));
                        writer.WriteString(query.Duration.ToString());
                        writer.WriteComma();

                        writer.WritePropertyName((nameof(query.QueryId)));
                        writer.WriteInteger(query.QueryId);
                        writer.WriteComma();

                        writer.WritePropertyName((nameof(query.StartTime)));
                        writer.WriteDateTime(query.StartTime);
                        writer.WriteComma();

                        writer.WritePropertyName((nameof(query.QueryInfo)));
                        writer.WriteIndexQuery(context, query.QueryInfo);

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return Task.CompletedTask;
        }
    }
}