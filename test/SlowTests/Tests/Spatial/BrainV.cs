using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Xunit;

namespace SlowTests.Tests.Spatial
{
    public class BrainV : RavenTestBase
    {
        [Fact]
        public void CanPerformSpatialSearchWithNulls()
        {
            using (var store = GetDocumentStore())
            {
                var indexDefinition = new IndexDefinition
                {
                    Name = "eventsByLatLng",
                    Maps = { "from e in docs.Events select new { Tag = \"Event\", Coordinates = CreateSpatialField(e.Latitude, e.Longitude) }" },
                    Fields = new Dictionary<string, IndexFieldOptions>
                    {
                        { "Tag", new IndexFieldOptions { Indexing = FieldIndexing.Exact }}
                    }
                };

                store.Admin.Send(new PutIndexesOperation(indexDefinition));

                using (var commands = store.Commands())
                {
                    commands.Put("Events/1", null, new
                    {
                        Venue = "Jimmy's Old Town Tavern",
                        Latitude = (double?)null,
                        Longitude = (double?)null
                    }, new Dictionary<string, object>
                    {
                        { Constants.Documents.Metadata.Collection, "Events" }
                    });
                }

                using (var session = store.OpenSession())
                {
                    var objects = session.Query<object>("eventsByLatLng")
                        .Customize(x => x.WaitForNonStaleResults())
                        .ToArray();

                    RavenTestHelper.AssertNoIndexErrors(store);

                    Assert.Equal(1, objects.Length);
                }
            }

        }


        [Fact]
        public void CanUseNullCoalescingOperator()
        {
            using (var store = GetDocumentStore())
            {
                var indexDefinition = new IndexDefinition
                {
                    Name = "eventsByLatLng",
                    Maps = { "from e in docs.Events select new { Tag = \"Event\", Coordinates = CreateSpatialField((e.Latitude ?? 38.9103000), e.Longitude ?? -77.3942) }" },
                    Fields = new Dictionary<string, IndexFieldOptions>
                    {
                        { "Tag", new IndexFieldOptions { Indexing = FieldIndexing.Exact }}
                    }
                };

                store.Admin.Send(new PutIndexesOperation(indexDefinition));

                using (var commands = store.Commands())
                {
                    commands.Put("Events/1", null, new
                    {
                        Venue = "Jimmy's Old Town Tavern",
                        Latitude = (double?)null,
                        Longitude = (double?)null
                    }, new Dictionary<string, object>
                    {
                        { Constants.Documents.Metadata.Collection, "Events" }
                    });
                }

                using (var session = store.OpenSession())
                {
                    var objects = session.Query<object>("eventsByLatLng")
                        .Spatial("Coordinates", x => x.WithinRadius(6, 38.9103000, -77.3942))
                        .Customize(x => x.WaitForNonStaleResults())
                        .ToArray();

                    RavenTestHelper.AssertNoIndexErrors(store);

                    Assert.Equal(1, objects.Length);
                }
            }

        }
    }
}
