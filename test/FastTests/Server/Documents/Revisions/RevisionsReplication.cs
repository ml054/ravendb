//-----------------------------------------------------------------------
// <copyright file="Revisions.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FastTests.Server.Replication;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Server.Documents;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace FastTests.Server.Documents.Revisions
{
    public class RevisionsReplication : ReplicationTestBase, IDocumentTombstoneAware
    {
        private void WaitForMarker(DocumentStore store1, DocumentStore store2)
        {
            var id = "marker - " + Guid.NewGuid();
            using (var session = store1.OpenSession())
            {
                session.Store(new Product { Name = "Marker" }, id);
                session.SaveChanges();
            }
            Assert.True(WaitForDocument(store2, id));
        }

        [Fact]
        public async Task CanGetAllRevisionsFor()
        {
            var company = new Company { Name = "Company Name" };
            using (var store1 = GetDocumentStore())
            using (var store2 = GetDocumentStore())
            {
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store1.Database);
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store2.Database);
                await SetupReplicationAsync(store1, store2);

                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(company);
                    await session.SaveChangesAsync();
                }
                using (var session = store1.OpenAsyncSession())
                {
                    var company3 = await session.LoadAsync<Company>(company.Id);
                    company3.Name = "Hibernating Rhinos";
                    await session.SaveChangesAsync();
                }
                WaitForMarker(store1, store2);
                using (var session = store2.OpenAsyncSession())
                {
                    var companiesRevisions = await session.Advanced.GetRevisionsForAsync<Company>(company.Id);
                    Assert.Equal(2, companiesRevisions.Count);
                    Assert.Equal("Hibernating Rhinos", companiesRevisions[0].Name);
                    Assert.Equal("Company Name", companiesRevisions[1].Name);
                }
            }
        }

        [Fact]
        public async Task CanCheckIfDocumentHasRevisions()
        {
            var company = new Company { Name = "Company Name" };
            using (var store1 = GetDocumentStore())
            using (var store2 = GetDocumentStore())
            {
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store1.Database);
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store2.Database);
                await SetupReplicationAsync(store1, store2);

                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(company);
                    await session.SaveChangesAsync();
                }
                WaitForMarker(store1, store2);
                using (var session = store2.OpenAsyncSession())
                {
                    var company3 = await session.LoadAsync<Company>(company.Id);
                    var metadata = session.Advanced.GetMetadataFor(company3);

                    Assert.Equal((DocumentFlags.HasRevisions | DocumentFlags.FromReplication).ToString(), metadata.GetString(Constants.Documents.Metadata.Flags));
                }
            }
        }

        [Fact]
        public async Task WillDeleteOldRevisions()
        {
            var company = new Company { Name = "Company #1" };
            using (var store1 = GetDocumentStore())
            using (var store2 = GetDocumentStore())
            {
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store1.Database);
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store2.Database);
                await SetupReplicationAsync(store1, store2);

                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(company);
                    await session.SaveChangesAsync();
                    for (var i = 0; i < 10; i++)
                    {
                        company.Name = "Company #2: " + i;
                        await session.SaveChangesAsync();
                    }
                }

                WaitForMarker(store1, store2);
                using (var session = store2.OpenAsyncSession())
                {
                    var revisions = await session.Advanced.GetRevisionsForAsync<Company>(company.Id);
                    Assert.Equal(5, revisions.Count);
                    Assert.Equal("Company #2: 9", revisions[0].Name);
                    Assert.Equal("Company #2: 5", revisions[4].Name);
                }
            }
        }

        [Fact]
        public async Task RevisionsOrder()
        {
            using (var store1 = GetDocumentStore())
            using (var store2 = GetDocumentStore())
            {
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store1.Database);
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store2.Database);
                await SetupReplicationAsync(store1, store2);

                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "Hibernating" }, "users/1");
                    await session.StoreAsync(new User { Name = "Hibernating11" }, "users/11");
                    await session.SaveChangesAsync();
                }
                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "Hibernating Rhinos" }, "users/1");
                    await session.StoreAsync(new User { Name = "Hibernating Rhinos11" }, "users/11");
                    await session.SaveChangesAsync();
                }
                using (var session = store1.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "Hibernating Rhinos - RavenDB" }, "users/1");
                    await session.StoreAsync(new User { Name = "Hibernating Rhinos - RavenDB11" }, "users/11");
                    await session.SaveChangesAsync();
                }

                WaitForMarker(store1, store2);
                using (var session = store2.OpenAsyncSession())
                {
                    var users = await session.Advanced.GetRevisionsForAsync<User>("users/1");
                    Assert.Equal(3, users.Count);
                    Assert.Equal("Hibernating Rhinos - RavenDB", users[0].Name);
                    Assert.Equal("Hibernating Rhinos", users[1].Name);
                    Assert.Equal("Hibernating", users[2].Name);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetRevisionsBinEntries(bool useSession)
        {
            using (var store1 = GetDocumentStore())
            using (var store2 = GetDocumentStore())
            {
                var database = await GetDocumentDatabaseInstanceFor(store1);
                var database2 = await GetDocumentDatabaseInstanceFor(store2);
                database.DocumentTombstoneCleaner.Subscribe(this);
                database2.DocumentTombstoneCleaner.Subscribe(this);

                await RevisionsHelper.SetupRevisions(Server.ServerStore, store1.Database, false);
                await RevisionsHelper.SetupRevisions(Server.ServerStore, store2.Database, false);
                await SetupReplicationAsync(store1, store2);

                var deletedRevisions = await store1.Commands().GetRevisionsBinEntriesAsync(long.MaxValue);
                Assert.Equal(0, deletedRevisions.Count);

                var id = "users/1";
                if (useSession)
                {
                    var user = new User {Name = "Fitzchak"};
                    for (var i = 0; i < 2; i++)
                    {
                        using (var session = store1.OpenAsyncSession())
                        {
                            await session.StoreAsync(user);
                            await session.SaveChangesAsync();
                        }
                        using (var session = store1.OpenAsyncSession())
                        {
                            session.Delete(user.Id);
                            await session.SaveChangesAsync();
                        }
                    }
                    id += "-A";
                }
                else
                {
                    await store1.Commands().PutAsync(id, null, new User {Name = "Fitzchak"});
                    await store1.Commands().DeleteAsync(id, null);
                    await store1.Commands().PutAsync(id, null, new User {Name = "Fitzchak"});
                    await store1.Commands().DeleteAsync(id, null);
                }

                WaitForMarker(store1, store2);
                var statistics = store2.Admin.Send(new GetStatisticsOperation());
                Assert.Equal(useSession ? 2 : 1, statistics.CountOfDocuments);
                Assert.Equal(4, statistics.CountOfRevisionDocuments);

                deletedRevisions = await store2.Commands().GetRevisionsBinEntriesAsync(long.MaxValue);
                Assert.Equal(1, deletedRevisions.Count);

                using (var session = store2.OpenAsyncSession())
                {
                    var users = await session.Advanced.GetRevisionsForAsync<User>(id);
                    Assert.Equal(4, users.Count);
                    Assert.Equal(null, users[0].Name);
                    Assert.Equal("Fitzchak", users[1].Name);
                    Assert.Equal(null, users[2].Name);
                    Assert.Equal("Fitzchak", users[3].Name);
                }

                // Can get metadata only
                dynamic revisions = await store2.Commands().GetRevisionsForAsync(id, metadataOnly: true);
                Assert.Equal(4, revisions.Count);
                Assert.Equal(DocumentFlags.DeleteRevision.ToString(), revisions[0][Constants.Documents.Metadata.Key][Constants.Documents.Metadata.Flags]);
                Assert.Equal((DocumentFlags.HasRevisions | DocumentFlags.Revision | DocumentFlags.FromReplication).ToString(), revisions[1][Constants.Documents.Metadata.Key][Constants.Documents.Metadata.Flags]);
                Assert.Equal(DocumentFlags.DeleteRevision.ToString(), revisions[2][Constants.Documents.Metadata.Key][Constants.Documents.Metadata.Flags]);
                Assert.Equal((DocumentFlags.HasRevisions | DocumentFlags.Revision | DocumentFlags.FromReplication).ToString(), revisions[3][Constants.Documents.Metadata.Key][Constants.Documents.Metadata.Flags]);

                await store1.Admin.SendAsync(new RevisionsTests.DeleteRevisionsOperation(id, "users/not/exists"));
                WaitForMarker(store1, store2);

                statistics = store2.Admin.Send(new GetStatisticsOperation());
                Assert.Equal(useSession ? 3 : 2, statistics.CountOfDocuments);
               
                Assert.Equal(0, statistics.CountOfRevisionDocuments); 
            }
        }

        public Task ReplicateExpiredAndDeletedRevisions(/*bool useSession*/)
        {
            // TODO
            return Task.CompletedTask;
        }

        private class User
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        private class Product
        {
            public string Name { get; set; }
        }

        public Dictionary<string, long> GetLastProcessedDocumentTombstonesPerCollection()
        {
            return new Dictionary<string, long>
            {
                ["Products"] = 0,
                ["Users"] = 0
            };
        }
    }
}