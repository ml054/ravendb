using System.Linq;
using FastTests;
using FastTests.Server.Basic.Entities;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace SlowTests.Tests.Bugs.Vlko
{
    public class QueryWithMultipleWhere : RavenTestBase
    {
        [Fact]
        public void ShouldGenerateProperPrecedence()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    var query = s.Query<User>()
                        .Where(x => x.Id == "1" || x.Id == "2" || x.Id == "3")
                        .Where(x => x.Age == 19);

                    var iq = RavenTestHelper.GetIndexQuery(query);

                    Assert.Equal("FROM Users WHERE ((id() = $p0 OR id() = $p1) OR id() = $p2) AND (Age = $p3)", iq.Query);
                    Assert.Equal("1", iq.QueryParameters["p0"]);
                    Assert.Equal("2", iq.QueryParameters["p1"]);
                    Assert.Equal("3", iq.QueryParameters["p2"]);
                    Assert.Equal(19, iq.QueryParameters["p3"]);
                }
            }
        }
    }
}
