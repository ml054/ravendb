using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Session;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDb827 : RavenTestBase
    {
        private class TranTest
        {
            public string Id { get; set; }
            public IDictionary<string, string> Trans { get; set; }
        }

        private class TranTestIndex : AbstractIndexCreationTask<TranTest>
        {
            public TranTestIndex()
            {
                Map = docs =>
                      from doc in docs
                      select new
                      {
                          _ = doc.Trans.Select(x => CreateField("Trans_" + x.Key, x.Value)),
                      };

                Index("Trans_en", FieldIndexing.Search);
                Index("Trans_fi", FieldIndexing.Search);
            }
        }

        [Fact]
        public void Can_Use_Dictionary_Created_Field_In_Lucene_Search()
        {
            using (var documentStore = GetDocumentStore())
            {
                documentStore.ExecuteIndex(new TranTestIndex());

                using (var session = documentStore.OpenSession())
                {
                    session.Store(new TranTest { Trans = new Dictionary<string, string> { { "en", "abc" }, { "fi", "def" } } });
                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    var searchTerms = "abc";

                    var query = session.Advanced.DocumentQuery<TranTest, TranTestIndex>()
                                       .WaitForNonStaleResults()
                                       .Search(x => x.Trans["en"], searchTerms);
                    var results = query.ToList();

                    Assert.Equal(1, results.Count);
                }
            }
        }

        [Fact]
        public void Can_Use_Dictionary_Created_Field_In_Linq_Where()
        {
            using (var documentStore = GetDocumentStore())
            {
                documentStore.ExecuteIndex(new TranTestIndex());

                using (var session = documentStore.OpenSession())
                {
                    session.Store(new TranTest { Trans = new Dictionary<string, string> { { "en", "abc" }, { "fi", "def" } } });
                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    var searchTerms = "abc";

                    var query = session.Query<TranTest, TranTestIndex>()
                                       .Customize(x => x.WaitForNonStaleResults())
                                       .Where(x => x.Trans["en"].StartsWith(searchTerms));

                    var results = query.ToList();

                    Assert.Equal(1, results.Count);
                }
            }
        }

        [Fact]
        public void Can_Use_Dictionary_Created_Field_In_Linq_Search()
        {
            using (var documentStore = GetDocumentStore())
            {
                documentStore.ExecuteIndex(new TranTestIndex());

                using (var session = documentStore.OpenSession())
                {
                    session.Store(new TranTest { Trans = new Dictionary<string, string> { { "en", "abc" }, { "fi", "def" } } });
                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    var searchTerms = "abc";

                    var query = session.Query<TranTest, TranTestIndex>()
                                       .Customize(x => x.WaitForNonStaleResults())
                                       .Search(x => x.Trans["en"], searchTerms);

                    var results = query.ToList();

                    Assert.Equal(1, results.Count);
                }
            }
        }

        [Fact]
        public void Can_Use_Dictionary_Created_Field_In_Linq_Search_Workaround()
        {
            using (var documentStore = GetDocumentStore())
            {
                documentStore.ExecuteIndex(new TranTestIndex());

                using (var session = documentStore.OpenSession())
                {
                    session.Store(new TranTest { Trans = new Dictionary<string, string> { { "en", "abc" }, { "fi", "def" } } });
                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    var searchTerms = "abc";

                    var query = session.Query<TranTest, TranTestIndex>()
                                       .Customize(x => x.WaitForNonStaleResults())
                                       .Customize(x => ((IDocumentQuery<TranTest>)x).Search(q => q.Trans["en"], searchTerms));

                    var results = query.ToList();

                    Assert.Equal(1, results.Count);
                }
            }
        }
    }
}
