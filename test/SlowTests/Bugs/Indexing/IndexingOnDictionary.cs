//-----------------------------------------------------------------------
// <copyright company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using FastTests;
using Xunit;
using System.Linq;

namespace SlowTests.Bugs.Indexing
{
    public class IndexingOnDictionary : RavenTestBase
    {
        [Fact]
        public void CanIndexValuesForDictionary()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new User
                    {
                        Items = new Dictionary<string, string>
                                                {
                                                    {"Color", "Red"}
                                                }
                    });

                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var users = s.Advanced.DocumentQuery<User>()
                        .WhereEquals("Items.Color", "Red")
                        .ToArray();
                    Assert.NotEmpty(users);
                }
            }
        }

        [Fact]
        public void CanIndexValuesForDictionaryAsPartOfDictionary()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new User
                    {
                        Items = new Dictionary<string, string>
                                                {
                                                    {"Color", "Red"}
                                                }
                    });

                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var users = s.Advanced.DocumentQuery<User>()
                        .WhereEquals("Items[].Key", "Color")
                        .AndAlso()
                        .WhereEquals("Items[].Value", "Red")
                        .ToArray();

                    Assert.NotEmpty(users);
                }
            }
        }

        [Fact]
        public void CanIndexNestedValuesForDictionaryAsPartOfDictionary()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new User
                    {
                        NestedItems = new Dictionary<string, NestedItem>
                        {
                            { "Color", new NestedItem{ Name="Red" } }
                        }
                    });
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var users = s.Advanced.DocumentQuery<User>()
                        .WhereEquals("NestedItems[].Key", "Color")
                        .AndAlso()
                        .WhereEquals("NestedItems[].Name", "Red")
                        .ToArray();
                    Assert.NotEmpty(users);
                }
            }
        }

        [Fact]
        public void CanIndexValuesForIDictionaryAsPartOfIDictionary()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new UserWithIDictionary
                    {
                        Items = new Dictionary<string, string>
                            {
                                { "Color", "Red" }
                            }
                    });
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var users = s.Advanced.DocumentQuery<UserWithIDictionary>()
                        .WhereEquals("Items[].Key", "Color")
                        .AndAlso()
                        .WhereEquals("Items[].Value", "Red")
                        .ToArray();
                    Assert.NotEmpty(users);
                }
            }
        }

        [Fact]
        public void CanIndexNestedValuesForIDictionaryAsPartOfIDictionary()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new UserWithIDictionary
                    {
                        NestedItems = new Dictionary<string, NestedItem>
                        {
                            { "Color", new NestedItem{ Name="Red" } }
                        }
                    });
                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var users = s.Advanced.DocumentQuery<UserWithIDictionary>()
                        .WhereEquals("NestedItems[].Key", "Color")
                        .AndAlso()
                        .WhereEquals("NestedItems[].Name", "Red")
                        .ToArray();
                    Assert.NotEmpty(users);
                }
            }
        }

        [Fact]
        public void CanIndexValuesForDictionaryWithNumberForIndex()
        {
            using (var store = GetDocumentStore())
            {
                using (var s = store.OpenSession())
                {
                    s.Store(new User
                    {
                        Items = new Dictionary<string, string>
                                                {
                                                    {"3", "Red"}
                                                }
                    });

                    s.SaveChanges();
                }

                using (var s = store.OpenSession())
                {
                    var users = s.Advanced.DocumentQuery<User>()
                        .WhereEquals("Items[].3", "Red")
                        .ToArray();
                    Assert.NotEmpty(users);
                }
            }
        }

        #region Nested type: User / UserWithIDictionary / NestedItem

        private class User
        {
            public string Id { get; set; }
            public Dictionary<string, string> Items { get; set; }
            public Dictionary<string, NestedItem> NestedItems { get; set; }
        }

        private class UserWithIDictionary
        {
            public string Id { get; set; }
            public IDictionary<string, string> Items { get; set; }
            public IDictionary<string, NestedItem> NestedItems { get; set; }
        }

        private class NestedItem { public string Name { get; set; } }

        #endregion
    }
}
