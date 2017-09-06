// -----------------------------------------------------------------------
//  <copyright file="Searching.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using FastTests;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Queries.Facets;
using Raven.Client.Documents.Queries.Suggestion;
using SlowTests.Core.Utils.Indexes;

using Xunit;

using Camera = SlowTests.Core.Utils.Entities.Camera;
using Event = SlowTests.Core.Utils.Entities.Event;
using Post = SlowTests.Core.Utils.Entities.Post;
using User = SlowTests.Core.Utils.Entities.User;

namespace SlowTests.Core.Querying
{
    public class Searching : RavenTestBase
    {
        [Fact]
        public void CanSearchByMultipleTerms()
        {
            using (var store = GetDocumentStore())
            {
                store.Admin.Send(new PutIndexesOperation(new[] {new IndexDefinition
                {
                    Maps = { "from post in docs.Posts select new { post.Title }" },
                    Fields = { { "Title", new IndexFieldOptions { Indexing = FieldIndexing.Search } } },
                    Name = "Posts/ByTitle"
                }}));

                using (var session = store.OpenSession())
                {
                    session.Store(new Post
                    {
                        Title = "Querying document database"
                    });

                    session.Store(new Post
                    {
                        Title = "Introduction to RavenDB"
                    });

                    session.Store(new Post
                    {
                        Title = "NOSQL databases"
                    });

                    session.Store(new Post
                    {
                        Title = "MSSQL 2012"
                    });

                    session.SaveChanges();

                    WaitForIndexing(store);

                    var aboutRavenDBDatabase =
                        session.Query<Post>("Posts/ByTitle")
                            .Search(x => x.Title, "database databases RavenDB")
                            .ToList();

                    Assert.Equal(3, aboutRavenDBDatabase.Count);

                    var exceptRavenDB =
                        session.Query<Post>("Posts/ByTitle")
                            .Search(x => x.Title, "RavenDB", options: SearchOptions.Not)
                            .ToList();

                    Assert.Equal(3, exceptRavenDB.Count);
                }
            }
        }

        [Fact]
        public void CanSearchByMultipleFields()
        {
            using (var store = GetDocumentStore())
            {
                store.Admin.Send(new PutIndexesOperation(new[] { new IndexDefinition
                {
                    Maps = { "from post in docs.Posts select new { post.Title, post.Desc }" },
                    Fields =
                    {
                        { "Title", new IndexFieldOptions { Indexing = FieldIndexing.Search} },
                        { "Desc", new IndexFieldOptions { Indexing = FieldIndexing.Search} }
                    },
                    Name = "Posts/ByTitleAndDescription"
                }}));

                using (var session = store.OpenSession())
                {
                    session.Store(new Post
                    {
                        Title = "RavenDB in action",
                        Desc = "Querying document database"
                    });

                    session.Store(new Post
                    {
                        Title = "Introduction to NOSQL",
                        Desc = "Modeling in document DB"
                    });

                    session.Store(new Post
                    {
                        Title = "MSSQL 2012"
                    });

                    session.SaveChanges();

                    WaitForIndexing(store);

                    var nosqlOrQuerying =
                        session.Query<Post>("Posts/ByTitleAndDescription")
                            .Search(x => x.Title, "nosql")
                            .Search(x => x.Desc, "querying")
                            .ToList();

                    Assert.Equal(2, nosqlOrQuerying.Count);
                    Assert.NotNull(nosqlOrQuerying.FirstOrDefault(x => x.Id == "posts/1-A"));
                    Assert.NotNull(nosqlOrQuerying.FirstOrDefault(x => x.Id == "posts/2-A"));

                    var notNosqlOrQuerying =
                        session.Query<Post>("Posts/ByTitleAndDescription")
                            .Search(x => x.Title, "nosql", options: SearchOptions.Not)
                            .Search(x => x.Desc, "querying")
                            .ToList();

                    Assert.Equal(2, notNosqlOrQuerying.Count);
                    Assert.NotNull(notNosqlOrQuerying.FirstOrDefault(x => x.Id == "posts/1-A"));
                    Assert.NotNull(notNosqlOrQuerying.FirstOrDefault(x => x.Id == "posts/3-A"));

                    var nosqlAndModeling =
                        session.Query<Post>("Posts/ByTitleAndDescription")
                            .Search(x => x.Title, "nosql")
                            .Search(x => x.Desc, "modeling", options: SearchOptions.And)
                            .ToList();

                    Assert.Equal(1, nosqlAndModeling.Count);
                    Assert.NotNull(nosqlAndModeling.FirstOrDefault(x => x.Id == "posts/2-A"));
                }
            }
        }

        [Fact]
        public void CanDoSpatialSearch()
        {
            using (var store = GetDocumentStore())
            {
                var eventsSpatialIndex = new Events_SpatialIndex();
                eventsSpatialIndex.Execute(store);

                using (var session = store.OpenSession())
                {
                    session.Store(new Event
                    {
                        Name = "Event1",
                        Latitude = 10.1234,
                        Longitude = 10.1234
                    });
                    session.Store(new Event
                    {
                        Name = "Event2",
                        Latitude = 0.3,
                        Longitude = 10.1234
                    });
                    session.Store(new Event
                    {
                        Name = "Event3",
                        Latitude = 19.1234,
                        Longitude = 10.789
                    });
                    session.Store(new Event
                    {
                        Name = "Event4",
                        Latitude = 10.1234,
                        Longitude = -0.2
                    });
                    session.Store(new Event
                    {
                        Name = "Event5",
                        Latitude = 10.1234,
                        Longitude = 19.789
                    });
                    session.Store(new Event
                    {
                        Name = "Event6",
                        Latitude = 60.1234,
                        Longitude = 19.789
                    });
                    session.Store(new Event
                    {
                        Name = "Event7",
                        Latitude = -60.1234,
                        Longitude = 19.789
                    });
                    session.Store(new Event
                    {
                        Name = "Event8",
                        Latitude = 10.1234,
                        Longitude = -19.789
                    });
                    session.Store(new Event
                    {
                        Name = "Event9",
                        Latitude = 10.1234,
                        Longitude = 79.789
                    });
                    session.SaveChanges();
                    WaitForIndexing(store);


                    var events = session.Query<Events_SpatialIndex.Result, Events_SpatialIndex>()
                        .Spatial(x => x.Coordinates, x => x.WithinRadius(1243.0, 10.1230, 10.1230))
                        .OrderBy(x => x.Name)
                        .OfType<Event>()
                        .ToArray();

                    Assert.Equal(5, events.Length);
                    Assert.Equal("Event1", events[0].Name);
                    Assert.Equal("Event2", events[1].Name);
                    Assert.Equal("Event3", events[2].Name);
                    Assert.Equal("Event4", events[3].Name);
                    Assert.Equal("Event5", events[4].Name);
                }
            }
        }

        [Fact]
        public void CanDoSearchBoosting()
        {
            using (var store = GetDocumentStore())
            {
                new Users_ByName().Execute(store);

                using (var session = store.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "Bob",
                        LastName = "LastName"
                    });
                    session.Store(new User
                    {
                        Name = "Name",
                        LastName = "LastName"
                    });
                    session.Store(new User
                    {
                        Name = "Name",
                        LastName = "Bob"
                    });
                    session.SaveChanges();
                    WaitForIndexing(store);

                    var users = session.Query<User, Users_ByName>()
                        .Where(x => x.Name == "Bob" || x.LastName == "Bob")
                        .ToArray();

                    Assert.Equal(2, users.Length);
                    Assert.Equal("Name", users[0].Name);
                    Assert.Equal("Bob", users[1].Name);
                }
            }
        }

        [Fact]
        public void CanProvideSuggestionsAndLazySuggestions()
        {
            using (var store = GetDocumentStore())
            {
                new Users_ByName().Execute(store);

                using (var session = store.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "John Smith"
                    });
                    session.Store(new User
                    {
                        Name = "Jack Johnson"
                    });
                    session.Store(new User
                    {
                        Name = "Robery Jones"
                    });
                    session.Store(new User
                    {
                        Name = "David Jones"
                    });
                    session.SaveChanges();
                    WaitForIndexing(store);

                    var users = session.Query<User, Users_ByName>()
                        .Where(x => x.Name == "johne");

                    SuggestionQueryResult suggestionResult = users.Suggest();
                    Assert.Equal(3, suggestionResult.Suggestions.Length);
                    Assert.Equal("john", suggestionResult.Suggestions[0]);
                    Assert.Equal("jones", suggestionResult.Suggestions[1]);
                    Assert.Equal("johnson", suggestionResult.Suggestions[2]);

                    Lazy<SuggestionQueryResult> lazySuggestionResult = users.SuggestLazy();

                    Assert.False(lazySuggestionResult.IsValueCreated);

                    suggestionResult = lazySuggestionResult.Value;

                    Assert.Equal(3, suggestionResult.Suggestions.Length);
                    Assert.Equal("john", suggestionResult.Suggestions[0]);
                    Assert.Equal("jones", suggestionResult.Suggestions[1]);
                    Assert.Equal("johnson", suggestionResult.Suggestions[2]);
                }
            }
        }

        [Fact]
        public void CanPerformFacetedSearchAndLazyFacatedSearch()
        {
            using (var store = GetDocumentStore())
            {
                new CameraCost().Execute(store);

                using (var session = store.OpenSession())
                {
                    for (int i = 0; i < 10; i++)
                    {
                        session.Store(new Camera
                        {
                            Id = "cameras/" + i,
                            Manufacturer = i % 2 == 0 ? "Manufacturer1" : "Manufacturer2",
                            Cost = i * 100D,
                            Megapixels = i * 1D
                        });
                    }

                    var facets = new List<Facet>
                    {
                        new Facet
                        {
                            Name = "Manufacturer"
                        },
                        new Facet
                        {
                            Name = "Cost_D_Range",
                            Mode = FacetMode.Ranges,
                            Ranges =
                            {
                                "[NULL TO 200.0]",
                                "[300.0 TO 400.0]",
                                "[500.0 TO 600.0]",
                                "[700.0 TO 800.0]",
                                "[900.0 TO NULL]"
                            }
                        },
                        new Facet
                        {
                            Name = "Megapixels_D_Range",
                            Mode = FacetMode.Ranges,
                            Ranges =
                            {
                                "[NULL TO 3.0]",
                                "[4.0 TO 7.0]",
                                "[8.0 TO 10.0]",
                                "[11.0 TO NULL]"
                            }
                        }
                    };
                    session.Store(new FacetSetup { Id = "facets/CameraFacets", Facets = facets });
                    session.SaveChanges();
                    WaitForIndexing(store);

                    var facetResults = session.Query<Camera, CameraCost>()
                        .ToFacets("facets/CameraFacets");

                    Assert.Equal(3, facetResults.Results.Count);

                    Assert.Equal(2, facetResults.Results["Manufacturer"].Values.Count);
                    Assert.Equal("manufacturer1", facetResults.Results["Manufacturer"].Values[0].Range);
                    Assert.Equal(5, facetResults.Results["Manufacturer"].Values[0].Hits);
                    Assert.Equal("manufacturer2", facetResults.Results["Manufacturer"].Values[1].Range);
                    Assert.Equal(5, facetResults.Results["Manufacturer"].Values[1].Hits);

                    Assert.Equal(5, facetResults.Results["Cost_D_Range"].Values.Count);
                    Assert.Equal("[NULL TO 200.0]", facetResults.Results["Cost_D_Range"].Values[0].Range);
                    Assert.Equal(3, facetResults.Results["Cost_D_Range"].Values[0].Hits);
                    Assert.Equal("[300.0 TO 400.0]", facetResults.Results["Cost_D_Range"].Values[1].Range);
                    Assert.Equal(2, facetResults.Results["Cost_D_Range"].Values[1].Hits);
                    Assert.Equal("[500.0 TO 600.0]", facetResults.Results["Cost_D_Range"].Values[2].Range);
                    Assert.Equal(2, facetResults.Results["Cost_D_Range"].Values[2].Hits);
                    Assert.Equal("[700.0 TO 800.0]", facetResults.Results["Cost_D_Range"].Values[3].Range);
                    Assert.Equal(2, facetResults.Results["Cost_D_Range"].Values[3].Hits);
                    Assert.Equal("[900.0 TO NULL]", facetResults.Results["Cost_D_Range"].Values[4].Range);
                    Assert.Equal(1, facetResults.Results["Cost_D_Range"].Values[4].Hits);

                    Assert.Equal(4, facetResults.Results["Megapixels_D_Range"].Values.Count);
                    Assert.Equal("[NULL TO 3.0]", facetResults.Results["Megapixels_D_Range"].Values[0].Range);
                    Assert.Equal(4, facetResults.Results["Megapixels_D_Range"].Values[0].Hits);
                    Assert.Equal("[4.0 TO 7.0]", facetResults.Results["Megapixels_D_Range"].Values[1].Range);
                    Assert.Equal(4, facetResults.Results["Megapixels_D_Range"].Values[1].Hits);
                    Assert.Equal("[8.0 TO 10.0]", facetResults.Results["Megapixels_D_Range"].Values[2].Range);
                    Assert.Equal(2, facetResults.Results["Megapixels_D_Range"].Values[2].Hits);
                    Assert.Equal("[11.0 TO NULL]", facetResults.Results["Megapixels_D_Range"].Values[3].Range);
                    Assert.Equal(0, facetResults.Results["Megapixels_D_Range"].Values[3].Hits);

                    var lazyFacetResults = session.Query<Camera, CameraCost>()
                        .ToFacetsLazy("facets/CameraFacets");

                    Assert.False(lazyFacetResults.IsValueCreated);

                    facetResults = lazyFacetResults.Value;

                    Assert.Equal(3, facetResults.Results.Count);

                    Assert.Equal(2, facetResults.Results["Manufacturer"].Values.Count);
                    Assert.Equal("manufacturer1", facetResults.Results["Manufacturer"].Values[0].Range);
                    Assert.Equal(5, facetResults.Results["Manufacturer"].Values[0].Hits);
                    Assert.Equal("manufacturer2", facetResults.Results["Manufacturer"].Values[1].Range);
                    Assert.Equal(5, facetResults.Results["Manufacturer"].Values[1].Hits);

                    Assert.Equal(5, facetResults.Results["Cost_D_Range"].Values.Count);
                    Assert.Equal("[NULL TO 200.0]", facetResults.Results["Cost_D_Range"].Values[0].Range);
                    Assert.Equal(3, facetResults.Results["Cost_D_Range"].Values[0].Hits);
                    Assert.Equal("[300.0 TO 400.0]", facetResults.Results["Cost_D_Range"].Values[1].Range);
                    Assert.Equal(2, facetResults.Results["Cost_D_Range"].Values[1].Hits);
                    Assert.Equal("[500.0 TO 600.0]", facetResults.Results["Cost_D_Range"].Values[2].Range);
                    Assert.Equal(2, facetResults.Results["Cost_D_Range"].Values[2].Hits);
                    Assert.Equal("[700.0 TO 800.0]", facetResults.Results["Cost_D_Range"].Values[3].Range);
                    Assert.Equal(2, facetResults.Results["Cost_D_Range"].Values[3].Hits);
                    Assert.Equal("[900.0 TO NULL]", facetResults.Results["Cost_D_Range"].Values[4].Range);
                    Assert.Equal(1, facetResults.Results["Cost_D_Range"].Values[4].Hits);

                    Assert.Equal(4, facetResults.Results["Megapixels_D_Range"].Values.Count);
                    Assert.Equal("[NULL TO 3.0]", facetResults.Results["Megapixels_D_Range"].Values[0].Range);
                    Assert.Equal(4, facetResults.Results["Megapixels_D_Range"].Values[0].Hits);
                    Assert.Equal("[4.0 TO 7.0]", facetResults.Results["Megapixels_D_Range"].Values[1].Range);
                    Assert.Equal(4, facetResults.Results["Megapixels_D_Range"].Values[1].Hits);
                    Assert.Equal("[8.0 TO 10.0]", facetResults.Results["Megapixels_D_Range"].Values[2].Range);
                    Assert.Equal(2, facetResults.Results["Megapixels_D_Range"].Values[2].Hits);
                    Assert.Equal("[11.0 TO NULL]", facetResults.Results["Megapixels_D_Range"].Values[3].Range);
                    Assert.Equal(0, facetResults.Results["Megapixels_D_Range"].Values[3].Hits);
                }
            }
        }
    }
}
