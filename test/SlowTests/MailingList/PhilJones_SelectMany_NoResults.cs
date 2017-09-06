// -----------------------------------------------------------------------
//  <copyright file="PhilJones_SelectMany_NoResults.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Xunit;

namespace SlowTests.MailingList
{
    public class PhilJones_SelectMany_NoResults : RavenTestBase
    {
        private class Service
        {
            public class EmailAddress
            {
                public string Email { get; set; }
                public string Notes { get; set; }
            }

            public string Name { get; set; }
            public string Description { get; set; }

            public List<EmailAddress> EmailAddresses { get; set; }
        }

        private class Services_QueryIndex : AbstractIndexCreationTask<Service, Services_QueryIndex.ReduceResult>
        {
            public class ReduceResult : Service
            {
                public object[] Query { get; set; }
            }

            public Services_QueryIndex()
            {
                Map = suppliers => from supplier in suppliers
                                   select new
                                   {
                                       supplier.Name,
                                       Query = new object[]
                                    {
                                        supplier.Name,
                                        supplier.EmailAddresses.Select(x => x.Email)
                                    }
                                   };

                Store(x => x.Query, FieldStorage.Yes);
                Indexes.Add(x => x.Query, FieldIndexing.Search);
            }
        }

        [Fact]
        public void SelectManyIndexReturnsResults()
        {
            using (var store = GetDocumentStore())
            {
                new Services_QueryIndex().Execute(store);

                using (var session = store.OpenSession())
                {
                    var service1 = new Service
                    {
                        Name = "Bob",
                        Description = "Bob's Service",
                        EmailAddresses = new List<Service.EmailAddress>
                        {
                            new Service.EmailAddress
                            {
                                Email = "bob@example.org",
                                Notes = "Bobby"
                            },
                            new Service.EmailAddress
                            {
                                Email = "Bobby@example.org",
                                Notes = "bobobo"
                            }
                        }
                    };

                    session.Store(service1);
                    session.SaveChanges();
                    var results = session.Query<Service, Services_QueryIndex>()
                        .Customize(x => x.WaitForNonStaleResultsAsOfNow())
                        .ToList();

                    Assert.Equal(1, results.Count);
                }
            }
        }
    }
}
