﻿using System.Linq;
using Raven.Client.Documents.Indexes;
using Xunit;

namespace FastTests.Client.Queries
{
    public class RangeQueryTest : RavenTestBase
    {

        [Fact]
        public void RangeQuery()
        {
            using (var store = GetDocumentStore())
            {

                store.ExecuteIndex(new AccommodationsIndex());

                using (var session = store.OpenSession())
                {
                    session.Store(new TestAccommodation
                    {
                        Id = "accommodation-1",
                        ExistsInLanguage = true,
                        ImageUrl = "http://google.com/favicon.ico",
                        Categories = "Category"
                    });

                    session.SaveChanges();
                }


                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {

                    var accs = session.Advanced.DocumentQuery<object, AccommodationsIndex>()
                        .Where("ImageUrl:[* TO *]")
                        .AndAlso()
                        .Where("Categories:[* TO *]")
                        .AndAlso()
                        .WhereEquals("ExistsInLanguage", true)
                        .ToList();

                    Assert.NotNull(accs);
                    Assert.Equal(1, accs.Count);
                }
            }

        }

        public class TestAccommodation
        {
            public string Id { get; set; }

            public decimal BestPrice { get; set; }

            public string PriceFrom { get; set; }

            public string ProductReference { get; set; }

            public string ImageUrl { get; set; }

            public bool ExistsInLanguage { get; set; }

            public string Categories { get; set; }
        }

        public class AccommodationsIndex : AbstractMultiMapIndexCreationTask<TestAccommodation>
        {
            public AccommodationsIndex()
            {
                AddMap<TestAccommodation>(accs => from acc in accs
                                                  select new
                                                  {
                                                      Id = acc.Id,
                                                      BestPrice = acc.BestPrice,
                                                      PriceFrom = acc.PriceFrom,
                                                      ProductReference = acc.ProductReference,
                                                      ImageUrl = acc.ImageUrl,
                                                      ExistsInLanguage = acc.ExistsInLanguage,
                                                      Categories = acc.Categories
                                                  });

            }

        }
    }
}