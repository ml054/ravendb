﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;
using Raven.Server.Config;
using Raven.Server.Documents.Indexes;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace FastTests.Server.Documents.Indexing
{
    public class ExactSearchOnAutoIndex_RavenDB_8006 : RavenTestBase
    {
        [Fact]
        public async Task CanUseExactInAutoIndex()
        {
            using (var store = GetDocumentStore(new Options
            {
                ModifyDatabaseRecord = record => record.Settings[RavenConfiguration.GetKey(x => x.Indexing.TimeBeforeDeletionOfSupersededAutoIndex)] = "0"
            }))
            {
                using (var s = store.OpenAsyncSession())
                {
                    await s.StoreAsync(new User
                    {
                        Name = "Ayende",
                        LastName = "Rahien"
                    });

                    await s.SaveChangesAsync();
                }

                using (var s = store.OpenAsyncSession())
                {
                    QueryStatistics stats;

                    // where exact(Name = 'Ayende')
                    var count = await s.Query<User>()
                        .Statistics(out stats)
                        .Where(u => u.Name == "Ayende", true)
                        .CountAsync();

                    Assert.Equal(1, count);
                    Assert.Equal("Auto/Users/ByExact(Name)", stats.IndexName);

                    // where Name = 'ayende'
                    count = await s.Query<User>()
                        .Statistics(out stats)
                        .Where(u => u.Name == "ayende")
                        .CountAsync();

                    Assert.Equal(1, count);
                    Assert.Equal("Auto/Users/ByExact(Name)", stats.IndexName);

                    // where exact(Name = 'Ayende') and search(LastName, '*en*')
                    // should extend mapping and remove prev index

                    count = await s.Query<User>()
                        .Statistics(out stats)
                        .Where(u => u.Name == "Ayende", true)
                        .Search(u => u.LastName, "*en*")
                        .CountAsync();

                    Assert.Equal(1, count);
                    Assert.Equal("Auto/Users/ByExact(Name)AndSearch(LastName)", stats.IndexName);
                }

                IndexInformation[] indexes = null;

                Assert.True(SpinWait.SpinUntil(() => (indexes = store.Admin.Send(new GetStatisticsOperation()).Indexes).Length == 1, 1000));

                Assert.Equal(1, indexes.Length);
                Assert.Equal("Auto/Users/ByExact(Name)AndSearch(LastName)", indexes[0].Name);
            }
        }

        [Fact]
        public async Task CanUseExactInAutoMapReduceIndex()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenAsyncSession())
                {
                    await s.StoreAsync(new User
                    {
                        Name = "Ayende Rahien"
                    });

                    await s.StoreAsync(new User
                    {
                        Name = "Ayende Rahien"
                    });

                    await s.SaveChangesAsync();
                }

                using (var s = store.OpenSession())
                {
                    QueryStatistics stats;

                    var results = s.Query<User>()
                        .Statistics(out stats)
                        .Customize(x => x.WaitForNonStaleResults())
                        .GroupBy(x => x.Name)
                        .Select(x => new
                        {
                            Name = x.Key,
                            Count = x.Count(),
                        })
                        .Where(x => x.Name == "Ayende Rahien", true)
                        .ToList();

                    Assert.Equal(1, results.Count);
                    Assert.Equal(2, results[0].Count);
                    Assert.Equal("Ayende Rahien", results[0].Name);

                    Assert.Equal("Auto/Users/ByCountReducedByExact(Name)", stats.IndexName);
                }

                using (var s = store.OpenSession())
                {
                    QueryStatistics stats;

                    var results = s.Query<User>()
                        .Statistics(out stats)
                        .Customize(x => x.WaitForNonStaleResults())
                        .GroupBy(x => x.Name)
                        .Select(x => new
                        {
                            Name = x.Key,
                            Count = x.Count(),
                        })
                        .Where(x => x.Name == "ayende")
                        .ToList();

                    Assert.Equal(0, results.Count);

                    Assert.Equal("Auto/Users/ByCountReducedByExact(Name)", stats.IndexName);
                }
            }
        }

        [Fact]
        public async Task ShouldExtendMappingOfTheSameField()
        {
            using (var store = GetDocumentStore(new Options
            {
                ModifyDatabaseRecord = record => record.Settings[RavenConfiguration.GetKey(x => x.Indexing.TimeBeforeDeletionOfSupersededAutoIndex)] = "0"
            }))
            {
                using (var s = store.OpenAsyncSession())
                {
                    await s.StoreAsync(new User
                    {
                        Name = "Ayende"
                    });

                    await s.SaveChangesAsync();
                }

                using (var s = store.OpenAsyncSession())
                {
                    QueryStatistics stats;

                    var count = await s.Query<User>()
                        .Statistics(out stats)
                        .Where(u => u.Name == "Ayende", true)
                        .CountAsync();

                    Assert.Equal(1, count);
                    Assert.Equal("Auto/Users/ByExact(Name)", stats.IndexName);

                    // where exact(Name = 'Ayende')
                    // should extend mapping and remove prev index

                    count = await s.Query<User>()
                        .Statistics(out stats)
                        .Search(u => u.Name, "*en*")
                        .CountAsync();

                    Assert.Equal(1, count);
                    Assert.Equal("Auto/Users/BySearch(Name)AndExact(Name)", stats.IndexName);
                }

                IndexInformation[] indexes = null;

                Assert.True(SpinWait.SpinUntil(() => (indexes = store.Admin.Send(new GetStatisticsOperation()).Indexes).Length == 1, 1000));

                Assert.Equal(1, indexes.Length);
                Assert.Equal("Auto/Users/BySearch(Name)AndExact(Name)", indexes[0].Name);
            }
        }

        [Fact]
        public async Task CanUseExactAndSearchTogetherInAutoMapReduceIndex()
        {
            using (var store = GetDocumentStore(new Options
            {
                ModifyDatabaseRecord = record => record.Settings[RavenConfiguration.GetKey(x => x.Indexing.TimeBeforeDeletionOfSupersededAutoIndex)] = "0"
            }))
            {
                using (var s = store.OpenAsyncSession())
                {
                    await s.StoreAsync(new User
                    {
                        Name = "Ayende",
                        LastName = "Rahien"
                    });

                    await s.StoreAsync(new User
                    {
                        Name = "Ayende",
                        LastName = "Rahien"
                    });

                    await s.SaveChangesAsync();
                }

                using (var s = store.OpenSession())
                {
                    QueryStatistics stats;

                    var results = s.Query<User>()
                        .Statistics(out stats)
                        .Customize(x => x.WaitForNonStaleResults())
                        .GroupBy(x => new { x.Name, x.LastName })
                        .Select(x => new
                        {
                            Name = x.Key.Name,
                            LastName = x.Key.LastName,
                            Count = x.Count(),
                        })
                        .Search(x => x.LastName, "*ah*")
                        .ToList();

                    Assert.Equal(1, results.Count);
                    Assert.Equal(2, results[0].Count);
                    Assert.Equal("Rahien", results[0].LastName);

                    Assert.Equal("Auto/Users/ByCountReducedByNameAndSearch(LastName)", stats.IndexName);

                    results = s.Query<User>()
                        .Statistics(out stats)
                        .Customize(x => x.WaitForNonStaleResults())
                        .GroupBy(x => new { x.Name, x.LastName })
                        .Select(x => new
                        {
                            Name = x.Key.Name,
                            LastName = x.Key.LastName,
                            Count = x.Count(),
                        })
                        .Where(x => x.Name == "Ayende", true)
                        .ToList();

                    Assert.Equal(1, results.Count);
                    Assert.Equal(2, results[0].Count);
                    Assert.Equal("Ayende", results[0].Name);

                    Assert.Equal("Auto/Users/ByCountReducedByExact(Name)AndSearch(LastName)", stats.IndexName);
                }

                IndexInformation[] indexes = null;

                Assert.True(SpinWait.SpinUntil(() => (indexes = store.Admin.Send(new GetStatisticsOperation()).Indexes).Length == 1, 1000));

                Assert.Equal(1, indexes.Length);
                Assert.Equal("Auto/Users/ByCountReducedByExact(Name)AndSearch(LastName)", indexes[0].Name);
            }
        }

        [Fact]
        public void IndexNameFinderShouldPreservePascalCaseFieldNames()
        {
            var name = AutoIndexNameFinder.FindMapIndexName("Users", new[]
            {
                new AutoIndexField()
                {
                    Name = "LastName",
                    Indexing = AutoFieldIndexing.Default | AutoFieldIndexing.Search
                }
            });

            Assert.Equal("Auto/Users/BySearch(LastName)", name);
        }
    }
}
