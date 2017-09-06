using System;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Linq;
using Xunit;

namespace SlowTests.MailingList
{
    public class WallaceTurner : RavenTestBase
    {
        private class DataResult
        {
            public DataResult()
            {
            }

            public string Id { get; set; }
            public string Address { get; set; }
            public string Price { get; set; }
            public string Url { get; set; }
            public string SiteId { get; set; }
            public string Source { get; set; }
            public DateTime CreatedOn { get; set; }
            public DateTime LastUpdated { get; set; }
            public DateTime LastModified { get; set; }
            public string State { get; set; }
            public string Postcode { get; set; }
            public string LowerIndicativePrice { get; set; }
            public string UpperIndicativePrice { get; set; }
            public string Suburb { get; set; }

            public override string ToString()
            {
                return string.Format("Address: {0}, Id: {1}, SiteId: {2}", Address, Id, SiteId);
            }

            public DataResult Clone()
            {
                return (DataResult)MemberwiseClone();
            }
        }

        private class DataResult_ByAddress : AbstractIndexCreationTask<DataResult>
        {
            public DataResult_ByAddress()
            {
                Map = docs => from doc in docs
                              select new
                              {
                                  LastModified = doc.LastModified,
                                  Address = doc.Address,
                                  Suburb = doc.Suburb,
                                  State = doc.State
                              };
                Index(x => x.Address, FieldIndexing.Search);
            }
        }

        [Fact]
        public void ShouldBeAbleToQueryUsingNull()
        {
            using (var store = GetDocumentStore())
            {
                new DataResult_ByAddress().Execute(store);
                using (var session = store.OpenSession())
                {
                    session.Store(new DataResult
                    {
                        SiteId = "t108137341"
                    });
                    session.SaveChanges();
                }

                using (var session = store.OpenSession())
                {
                    var dataResult = session.Query<DataResult>().First(r => r.SiteId == "t108137341");
                    Assert.Null(dataResult.State);
                    var results = session.Query<DataResult>().Where(r => r.State == null)
                        .Customize(o => o.WaitForNonStaleResults()).ToList();

                    Assert.NotEmpty(results);
                }
            }
        }
    }
}
