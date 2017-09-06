// -----------------------------------------------------------------------
//  <copyright file="linmouhong.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using FastTests;
using Xunit;

namespace SlowTests.MailingList
{
    public class linmouhong : RavenTestBase
    {
        private class Item
        {
#pragma warning disable 649
            public Product Product;
#pragma warning restore 649
        }

        private class Product
        {
#pragma warning disable 649
            public string Name;
#pragma warning restore 649
        }

        [Fact]
        public void CanCreateProperNestedQuery()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    var s = session.Advanced.DocumentQuery<Item>("test").WhereEquals(x => x.Product.Name, "test").GetIndexQuery();

                    Assert.Equal("FROM INDEX 'test' WHERE Product_Name = $p0", s.Query);
                    Assert.Equal("test", s.QueryParameters["p0"]);

                    s = session.Advanced.DocumentQuery<Item>().WhereEquals(x => x.Product.Name, "test").GetIndexQuery();

                    Assert.Equal("FROM Items WHERE Product.Name = $p0", s.Query);
                    Assert.Equal("test", s.QueryParameters["p0"]);
                }
            }
        }
    }
}
