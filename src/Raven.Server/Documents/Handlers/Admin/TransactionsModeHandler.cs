﻿using System;
using System.Threading.Tasks;
using Raven.Server.Routing;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Voron;

namespace Raven.Server.Documents.Handlers.Admin
{
    public class TransactionsModeHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/admin/transactions-mode", "GET", AuthorizationStatus.Operator)]
        public Task CommitNonLazyTx()
        {
            var modeStr = GetQueryStringValueAndAssertIfSingleAndNotEmpty("mode");
            if (Enum.TryParse(modeStr, true, out TransactionsMode mode) == false)
                throw new InvalidOperationException("Query string value 'mode' is not a valid mode: " + modeStr);

            var configDuration = Database.Configuration.Storage.TransactionsModeDuration.AsTimeSpan;
            var duration = GetTimeSpanQueryString("duration", required: false) ?? configDuration;
            using (ContextPool.AllocateOperationContext(out JsonOperationContext context))
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(("Environments"));
                writer.WriteStartArray();
                bool first = true;
                foreach (var storageEnvironment in Database.GetAllStoragesEnvironment())
                {
                    if (storageEnvironment == null)
                        continue;

                    if (first == false)
                    {
                        writer.WriteComma();
                    }
                    first = false;

                    var result = storageEnvironment.Environment.SetTransactionMode(mode, duration);
                    switch (result)
                    {
                        case TransactionsModeResult.ModeAlreadySet:
                            context.Write(writer, new DynamicJsonValue
                            {
                                ["Type"] = mode.ToString(),
                                ["Path"] = storageEnvironment.Environment.Options.BasePath,
                                ["Result"] = "Mode Already Set"
                            });
                            break;
                        case TransactionsModeResult.SetModeSuccessfully:
                            context.Write(writer, new DynamicJsonValue
                            {
                                ["Type"] = mode.ToString(),
                                ["Path"] = storageEnvironment.Environment.Options.BasePath,
                                ["Result"] = "Mode Set Successfully"
                            });
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("Result is unexpected value: " + result);
                    }
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return Task.CompletedTask;
        }
    }
}
