﻿using FastTests;
using Raven.Client.Documents.Queries;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_8805 : RavenTestBase
    {
        [Fact]
        public void ShouldNotThrow()
        {
            using (var store = GetDocumentStore())
            {
                using (var commands = store.Commands())
                {
                    commands.Query(new IndexQuery
                    {
                        Query = "from Orders where Freight in (11.61)"
                    });
                }
            }
        }
    }
}
