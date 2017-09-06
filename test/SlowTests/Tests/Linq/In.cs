// -----------------------------------------------------------------------
//  <copyright file="In.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Linq;
using Xunit;

namespace SlowTests.Tests.Linq
{
    public class In : RavenTestBase
    {
        private class Document
        {
            public string Name { get; set; }
            public Guid OwnerId { get; set; }
            public IEnumerable<Guid> PermittedUsers { get; set; }
        }

        private class User
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        private class SearchableElement
        {
            public object[] PermittedUsers { get; set; }
        }

        private class SearchableElements : AbstractMultiMapIndexCreationTask<SearchableElement>
        {
            public SearchableElements()
            {
                AddMap<Document>(documents => from doc in documents
                                              select new SearchableElement
                                              {
                                                  PermittedUsers = new object[]
                                                  {
                                                      doc.OwnerId,
                                                      doc.PermittedUsers,
                                                  }
                                              });
            }
        }

        private readonly Guid userId = new Guid("dc89a428-7eb2-428c-bc97-99763db25f9a");

        [Fact]
        public void WithNotEmptyObjectsArray()
        {
            using (var store = GetDocumentStore())
            {
                new SearchableElements().Execute(store);

                using (var session = store.OpenSession())
                {
                    var count1 = session.Query<SearchableElement, SearchableElements>()
                                        .Count(se => se.PermittedUsers.In(new object[] { userId }));
                    Assert.Equal(0, count1);


                    var count2 = session.Query<SearchableElement, SearchableElements>()
                                        .Count(se => se.PermittedUsers.Any(u => u.In(new object[] { userId })));
                    Assert.Equal(0, count2);

                    var query1 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.In(new object[] { userId }));

                    var iq = RavenTestHelper.GetIndexQuery(query1);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Contains(userId, (object[])iq.QueryParameters["p0"]);

                    var query2 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.Any(u => u.In(new object[] { userId })));

                    iq = RavenTestHelper.GetIndexQuery(query2);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Contains(userId, (object[])iq.QueryParameters["p0"]);
                }
            }
        }

        private readonly string[] _users = { "a-A 1", " -", "- " };

        [Fact]
        public void CanQueryEvilDashStrings()
        {
            using (var store = GetDocumentStore())
            {
                new SearchableElements().Execute(store);

                using (var session = store.OpenSession())
                {
                    session.Store(new User() { Id = "users/1", Name = "a-A 1" });
                    session.Store(new User() { Id = "users/2", Name = " -" });
                    session.Store(new User() { Id = "users/3", Name = "- " });
                    session.SaveChanges();
                    var res = session.Query<User>().Where(user => user.Name.In(_users)).ToList();
                    Assert.Equal(res.Count, 3);
                }
            }
        }

        [Fact]
        public void WithNotEmptyGuidsArray()
        {
            using (var store = GetDocumentStore())
            {
                new SearchableElements().Execute(store);

                using (var session = store.OpenSession())
                {
                    var count1 = session.Query<SearchableElement, SearchableElements>()
                                        .Count(se => se.PermittedUsers.In(new Guid[] { userId }.Cast<object>()));
                    Assert.Equal(0, count1);


                    var count2 = session.Query<SearchableElement, SearchableElements>()
                        .Count(se => se.PermittedUsers.Any(u => u.In(new Guid[] { userId }.Cast<object>())));
                    Assert.Equal(0, count2);

                    var query1 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.In(new Guid[] { userId }.Cast<object>()));

                    var iq = RavenTestHelper.GetIndexQuery(query1);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Contains(userId, (object[])iq.QueryParameters["p0"]);

                    var query2 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.Any(u => u.In(new Guid[] { userId }.Cast<object>())));

                    iq = RavenTestHelper.GetIndexQuery(query2);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Contains(userId, (object[])iq.QueryParameters["p0"]);
                }
            }
        }

        [Fact]
        public void WithEmptyObjectsArray()
        {
            using (var store = GetDocumentStore())
            {
                new SearchableElements().Execute(store);

                using (var session = store.OpenSession())
                {
                    var count1 = session.Query<SearchableElement, SearchableElements>()
                                        .Count(se => se.PermittedUsers.In(new object[0]));
                    Assert.Equal(0, count1);


                    var count2 = session.Query<SearchableElement, SearchableElements>()
                                        .Count(se => se.PermittedUsers.Any(u => u.In(new object[0])));
                    Assert.Equal(0, count2);

                    var query1 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.In(new object[0]));

                    var iq = RavenTestHelper.GetIndexQuery(query1);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Empty((object[])iq.QueryParameters["p0"]);

                    var query2 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.Any(u => u.In(new object[0])));

                    iq = RavenTestHelper.GetIndexQuery(query2);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Empty((object[])iq.QueryParameters["p0"]);
                }
            }
        }

        [Fact]
        public void WithEmptyGuidsArray()
        {
            using (var store = GetDocumentStore())
            {
                new SearchableElements().Execute(store);

                using (var session = store.OpenSession())
                {
                    var count1 = session.Query<SearchableElement, SearchableElements>()
                                        .Count(se => se.PermittedUsers.In(new Guid[0].Cast<object>()));
                    Assert.Equal(0, count1);


                    var count2 = session.Query<SearchableElement, SearchableElements>()
                        .Count(se => se.PermittedUsers.Any(u => u.In(new Guid[0].Cast<object>())));
                    Assert.Equal(0, count2);

                    var query1 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.In(new Guid[0].Cast<object>()));

                    var iq = RavenTestHelper.GetIndexQuery(query1);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Empty((object[])iq.QueryParameters["p0"]);

                    var query2 = session.Query<SearchableElement, SearchableElements>()
                        .Where(se => se.PermittedUsers.Any(u => u.In(new Guid[0].Cast<object>())));

                    iq = RavenTestHelper.GetIndexQuery(query2);
                    Assert.Equal("FROM INDEX 'SearchableElements' WHERE PermittedUsers IN ($p0)", iq.Query);
                    Assert.Empty((object[])iq.QueryParameters["p0"]);
                }
            }
        }
    }
}
