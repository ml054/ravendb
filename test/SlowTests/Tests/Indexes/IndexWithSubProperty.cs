using System.Linq;
using FastTests;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Indexes;
using Xunit;

namespace SlowTests.Tests.Indexes
{
    public class IndexWithSubProperty : RavenTestBase
    {
        [Fact]
        public void IndexWithSubPropertyReturnAs_Property_SubProperty()
        {
            var index = new ContactIndex
            {
                Conventions = new DocumentConventions()
            };
            var indexDefinition = index.CreateIndexDefinition();

            Assert.True(indexDefinition.Fields.ContainsKey("PrimaryEmail_Email"));
            Assert.Equal(FieldStorage.Yes, indexDefinition.Fields["PrimaryEmail_Email"].Storage.Value);
            Assert.Equal(FieldIndexing.Search, indexDefinition.Fields["PrimaryEmail_Email"].Indexing.Value);
            Assert.Equal("SimpleAnalyzer", indexDefinition.Fields["PrimaryEmail_Email"].Analyzer);

            Assert.True(indexDefinition.Fields.ContainsKey("String_Store"));
            Assert.Equal(FieldStorage.Yes, indexDefinition.Fields["String_Store"].Storage.Value);

            Assert.True(indexDefinition.Fields.ContainsKey("String_Index"));
            Assert.Equal(FieldIndexing.Search, indexDefinition.Fields["String_Index"].Indexing.Value);

            Assert.True(indexDefinition.Fields.ContainsKey("String_Analyzer"));
            Assert.Equal("SnowballAnalyzer", indexDefinition.Fields["String_Analyzer"].Analyzer);
        }

        private class ContactIndex : AbstractIndexCreationTask<Contact>
        {
            public ContactIndex()
            {
                Map = contacts => from contact in contacts
                                  select new
                                  {
                                      contact.FirstName,
                                      PrimaryEmail_EmailAddress = contact.PrimaryEmail.Email,
                                  };

                Store("String_Store", FieldStorage.Yes);
                Store(x => x.PrimaryEmail.Email, FieldStorage.Yes);
                Index(x => x.PrimaryEmail.Email, FieldIndexing.Search);
                Index("String_Index", FieldIndexing.Search);
                Analyze(x => x.PrimaryEmail.Email, "SimpleAnalyzer");
                Analyze("String_Analyzer", "SnowballAnalyzer");
            }
        }

        private class Contact
        {
            public string Id { get; set; }
            public string FirstName { get; set; }
            public string Surname { get; set; }
            public EmailAddress PrimaryEmail { get; set; }
        }

        private class EmailAddress
        {
            public string Email { get; set; }
        }
    }
}
