﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FastTests.Server.Replication;
using Raven.Client.ServerWide.Operations.Certificates;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace SlowTests.Server.Replication
{
    public class ReplicationBasicTestsSlow : ReplicationTestBase
    {
        public readonly string DbName = "TestDB" + Guid.NewGuid();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Master_master_replication_from_etag_zero_without_conflict_should_work(bool useSsl)
        {
            var dbName1 = DbName + "-1";
            var dbName2 = DbName + "-2";

            X509Certificate2 clientCertificate = null;
            X509Certificate2 adminCertificate = null;
            if (useSsl)
            {
                var serverCertPath = SetupServerAuthentication();
                adminCertificate = AskServerForClientCertificate(serverCertPath, new Dictionary<string, DatabaseAccess>(), SecurityClearance.ClusterAdmin);
                clientCertificate = AskServerForClientCertificate(serverCertPath, new Dictionary<string, DatabaseAccess>(), SecurityClearance.Operator);
            }

            using (var store1 = GetDocumentStore(new Options
            {
                AdminCertificate = adminCertificate,
                ClientCertificate = clientCertificate,
                ModifyDatabaseName = s => dbName1
            }))
            using (var store2 = GetDocumentStore(new Options
            {
                AdminCertificate = adminCertificate,
                ClientCertificate = clientCertificate,
                ModifyDatabaseName = s => dbName2
            }))
            {
                await SetupReplicationAsync(store1, store2);
                await SetupReplicationAsync(store2, store1);
                using (var session = store1.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "John Dow",
                        Age = 30
                    }, "users/1");
                    session.SaveChanges();
                }
                using (var session = store2.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "Jane Dow",
                        Age = 31
                    }, "users/2");

                    session.SaveChanges();
                }

                var replicated1 = WaitForDocumentToReplicate<User>(store1, "users/1", 10000);

                Assert.NotNull(replicated1);
                Assert.Equal("John Dow", replicated1.Name);
                Assert.Equal(30, replicated1.Age);

                var replicated2 = WaitForDocumentToReplicate<User>(store1, "users/2", 10000);
                Assert.NotNull(replicated2);
                Assert.Equal("Jane Dow", replicated2.Name);
                Assert.Equal(31, replicated2.Age);

                replicated1 = WaitForDocumentToReplicate<User>(store2, "users/1", 10000);

                Assert.NotNull(replicated1);
                Assert.Equal("John Dow", replicated1.Name);
                Assert.Equal(30, replicated1.Age);

                replicated2 = WaitForDocumentToReplicate<User>(store2, "users/2", 10000);
                Assert.NotNull(replicated2);
                Assert.Equal("Jane Dow", replicated2.Name);
                Assert.Equal(31, replicated2.Age);
            }
        }

    }
}
