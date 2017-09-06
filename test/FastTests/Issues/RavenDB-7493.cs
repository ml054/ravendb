﻿using System;
using System.Linq;
using Raven.Client.Json.Converters;
using Raven.Server.Json;
using Raven.Server.ServerWide;
using Raven.Server.Utils;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Xunit;

namespace FastTests.Issues
{
    public class RavenDB_7493 : NoDisposalNeeded
    {
        [Fact]
        public void Ensure_deserialization_routines_are_properly_created()
        {
            using (var context = JsonOperationContext.ShortTermSingleUse())
            {
                var djv = new DynamicJsonValue();

                var emptyBlittable = context.ReadObject(djv, "goo");

                var ea = new ExceptionAggregator("Could not create deserialization routines");

                foreach (var type in new []{ typeof(JsonDeserializationClient), typeof(JsonDeserializationCluster), typeof(JsonDeserializationServer) })
                {
                    foreach (var field in type.GetFields().Where(x => x.IsStatic))
                    {
                        var value = field.GetValue(null);

                        var func = value as Func<BlittableJsonReaderObject, object>;

                        if (func == null)
                            continue;

                        ea.Execute(() =>
                        {
                            try
                            {
                                func(emptyBlittable);
                            }
                            catch (Exception e) when (e.ToString().Contains("Failed to fetch property name")) // due to empty json we pass here, let's ignore it
                            {
                            }
                        });
                    }
                }

                foreach (var command in JsonDeserializationCluster.Commands.Values)
                {
                    ea.Execute(() =>
                    {
                        try
                        {
                            command(emptyBlittable);
                        }
                        catch (Exception e) when (e.ToString().Contains("Failed to fetch property name")) // due to empty json we pass here, let's ignore it
                        {
                        }
                    });
                }

                ea.ThrowIfNeeded();
            }
        }
    }
}
