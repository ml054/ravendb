// -----------------------------------------------------------------------
//  <copyright file="RavenDB_2607.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Queries;
using SlowTests.Core.Utils.Entities;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_2607 : RavenTestBase
    {
        private class CompaniesIndex : AbstractIndexCreationTask<Company>
        {
            public CompaniesIndex()
            {
                Map = companies => from c in companies
                                   select new
                                   {
                                       c.Name
                                   };
            }
        }

        public class UsersIndex : AbstractIndexCreationTask<User>
        {
            public UsersIndex()
            {
                Map = users => from user in users
                               select new
                               {
                                   user.Name
                               };
            }
        }

        public class Foo
        {
            public string Name { get; set; }
        }

        public class UsersAndCompaniesIndex : AbstractMultiMapIndexCreationTask<Foo>
        {
            public UsersAndCompaniesIndex()
            {
                AddMap<Company>(companies => from c in companies
                                             select new
                                             {
                                                 c.Name
                                             });

                AddMap<User>(users => from user in users
                                      select new
                                      {
                                          user.Name
                                      });
            }
        }

        [Fact]
        public void AddingUnrelevantDocumentForIndexShouldNotMarkItAsStale()
        {
            using (var store = GetDocumentStore())
            {
                var companiesIndex = new CompaniesIndex();
                var usersIndex = new UsersIndex();
                var usersAndCompaniesIndex = new UsersAndCompaniesIndex();

                store.ExecuteIndex(companiesIndex);
                store.ExecuteIndex(usersIndex);
                store.ExecuteIndex(usersAndCompaniesIndex);

                using (var session = store.OpenSession())
                {
                    session.Store(new Company
                    {
                        Name = "A"
                    });

                    session.Store(new User
                    {
                        Name = "A"
                    });

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                store.Admin.Send(new StopIndexingOperation());

                using (var session = store.OpenSession())
                {
                    session.Store(new Company
                    {
                        Name = "B"
                    });

                    session.SaveChanges();
                }

                var databaseStatistics = store.Admin.Send(new GetStatisticsOperation());

                Assert.Equal(2, databaseStatistics.StaleIndexes.Length);
                Assert.Contains(companiesIndex.IndexName, databaseStatistics.StaleIndexes);
                Assert.Contains(usersAndCompaniesIndex.IndexName, databaseStatistics.StaleIndexes);

                using (var commands = store.Commands())
                {
                    var queryResult = commands.Query(usersIndex.IndexName, new IndexQuery());

                    Assert.False(queryResult.IsStale);
                    Assert.True(queryResult.Results.Length > 0);

                    queryResult = commands.Query(companiesIndex.IndexName, new IndexQuery());
                    Assert.True(queryResult.IsStale);

                    queryResult = commands.Query(usersAndCompaniesIndex.IndexName, new IndexQuery());
                    Assert.True(queryResult.IsStale);
                }
            }
        }

        [Fact]
        public void MustNotShowThatIndexIsNonStale_BulkInsertCase()
        {
            using (var store = GetDocumentStore())
            {
                var companiesIndex = new CompaniesIndex();
                var usersIndex = new UsersIndex();
                var usersAndCompaniesIndex = new UsersAndCompaniesIndex();

                store.ExecuteIndex(companiesIndex);
                store.ExecuteIndex(usersIndex);
                store.ExecuteIndex(usersAndCompaniesIndex);

                using (var session = store.OpenSession())
                {
                    session.Store(new Company
                    {
                        Name = "A"
                    });

                    session.Store(new User
                    {
                        Name = "A"
                    });

                    session.SaveChanges();
                }

                using (var bulk = store.BulkInsert())
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        bulk.Store(new Company
                        {
                            Name = "A"
                        });

                        bulk.Store(new User
                        {
                            Name = "A"
                        });
                    }
                }

                using (var session = store.OpenSession())
                {
                    Assert.Equal(2002, session.Query<Foo, UsersAndCompaniesIndex>().Customize(x => x.WaitForNonStaleResults()).Count());
                }
            }
        }
    }
}
