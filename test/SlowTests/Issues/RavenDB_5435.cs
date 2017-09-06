﻿using System;
using System.Linq;
using System.Threading.Tasks;
using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using SlowTests.Core.Utils.Entities;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_5435 : RavenTestBase
    {
        private class Users_ByName : AbstractIndexCreationTask<User>
        {
            public Users_ByName()
            {
                Map = users => from u in users
                               select new
                               {
                                   u.Name
                               };
            }
        }

        [Fact]
        public async Task CanCompact()
        {
            var path = NewDataPath();
            using (var store = GetDocumentStore(new Options
            {
                Path = path
            }))
            {
                new Users_ByName().Execute(store);

                using (var session = store.OpenSession())
                {
                    for (var i = 0; i < 1000; i++)
                    {
                        session.Store(new User
                        {
                            Name = i.ToString()
                        });
                    }

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                var operation = store.Admin.Send(new CompactIndexOperation(new Users_ByName().IndexName));
                await operation.WaitForCompletionAsync(TimeSpan.FromSeconds(15));

                using (var session = store.OpenSession())
                {
                    var count = session.Query<User, Users_ByName>().Count();
                    Assert.Equal(1000, count);
                }
            }
        }
    }
}
