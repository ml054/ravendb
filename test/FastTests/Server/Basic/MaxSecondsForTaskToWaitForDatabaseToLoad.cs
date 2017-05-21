﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Conventions;
using Raven.Client.Exceptions.Database;
using Raven.Client.Http;
using Raven.Client.Server;
using Raven.Client.Server.Operations;
using Raven.Server.Config;
using Raven.Server.Config.Settings;
using Sparrow.Json;
using Xunit;

namespace FastTests.Server.Basic
{
    public class MaxSecondsForTaskToWaitForDatabaseToLoad : RavenTestBase
    {
        [Fact]
        public void Should_not_throw_when_there_no_timeout()
        {
            UseNewLocalServer();
            Server.Configuration.Server.MaxTimeForTaskToWaitForDatabaseToLoad = new TimeSetting(10, TimeUnit.Seconds);

            int retries = 3;
            //in case that there is alot of stuff is going on concurrently with this test,
            //give several chances for the load to pass successfully 
            bool didPassAtLeastOnce = false;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var store = GetDocumentStore())
                    using (var session = store.OpenSession())
                    {
                        session.Load<object>("Raven/ServerPrefixForHilo");
                    }
                    didPassAtLeastOnce = true;
                    break;
                }
                catch (DatabaseLoadTimeoutException)
                {
                    if (--retries == 0)
                        throw;
                }
            }

            Assert.True(didPassAtLeastOnce);
        }

        [Fact]
        public void Should_throw_when_there_is_timeout()
        {
            UseNewLocalServer();
            Server.Configuration.Server.MaxTimeForTaskToWaitForDatabaseToLoad = new TimeSetting(0, TimeUnit.Milliseconds);
            Server.ServerStore.DatabasesLandlord.OnDatabaseLoaded += s => Thread.Sleep(100); // force timeout          

            var url = Server.WebUrls[0];
            var name = Guid.NewGuid().ToString();
            var doc = GenerateDatabaseDoc(name);

            try
            {
                Assert.Throws<DatabaseLoadTimeoutException>(() =>
                {
                    using (var ctx = JsonOperationContext.ShortTermSingleUse())
                    using (var reqExecuter = RequestExecutor.CreateForSingleNode(url, name, null))
                    {
                        reqExecuter.Execute(
                            new CreateDatabaseOperation(doc).GetCommand(new DocumentConventions(), ctx), ctx);
                        reqExecuter.Execute(new GetDocumentCommand("Raven/HiloPrefix", includes: null, transformer: null, transformerParameters: null, metadataOnly: false), ctx);
                    }
                });
            }
            catch (TaskCanceledException e)
            {
                Console.WriteLine(e.InnerException);
                throw;
            }
        }

        private static DatabaseRecord GenerateDatabaseDoc(string name)
        {
            var doc = MultiDatabase.CreateDatabaseDocument(name);
            doc.Settings[RavenConfiguration.GetKey(x => x.Replication.ReplicationMinimalHeartbeat)] = "10";
            doc.Settings[RavenConfiguration.GetKey(x => x.Core.RunInMemory)] = "true";
            doc.Settings[RavenConfiguration.GetKey(x => x.Core.DataDirectory)] = $"\\{name}";
            doc.Settings[RavenConfiguration.GetKey(x => x.Core.ThrowIfAnyIndexOrTransformerCouldNotBeOpened)] = "true";
            doc.Settings[RavenConfiguration.GetKey(x => x.Indexing.MinNumberOfMapAttemptsAfterWhichBatchWillBeCanceledIfRunningLowOnMemory)] = int.MaxValue.ToString();

            return doc;
        }
    }
}
