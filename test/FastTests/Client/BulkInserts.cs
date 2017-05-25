﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Indexes;

using Sparrow.Json;
using Xunit;

namespace FastTests.Client
{
    public class BulkInserts : RavenTestBase
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Simple_Bulk_Insert(bool useSsl)
        {
            if (useSsl)
            {
                DoNotReuseServer(new ConcurrentDictionary<string, string> { ["Raven/UseSsl"] = "true" });
            }
            using (var store = GetDocumentStore())
            {
                using (var bulkInsert = store.BulkInsert())
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        await bulkInsert.StoreAsync(new FooBar() { Name = "foobar/" + i }, "FooBars/" + i);
                    }
                }


                using (var session = store.OpenSession())
                {
                    var len = session.Advanced.LoadStartingWith<FooBar>("FooBars/", null, 0, 1000, null);
                    Assert.Equal(1000, len.Length);
                }
            }
        }

        [Fact]
        public async Task Simple_Bulk_Insert_Should_Work()
        {
            var fooBars = new[]
            {
                new FooBar { Name = "John Doe" },
                new FooBar { Name = "Jane Doe" },
                new FooBar { Name = "Mega John" },
                new FooBar { Name = "Mega Jane" }
            };
            using (var store = GetDocumentStore())
            {
                using (var bulkInsert = store.BulkInsert())
                {
                    await bulkInsert.StoreAsync(fooBars[0]);
                    await bulkInsert.StoreAsync(fooBars[1]);
                    await bulkInsert.StoreAsync(fooBars[2]);
                    await bulkInsert.StoreAsync(fooBars[3]);
                }

                store.GetRequestExecutor(store.Database).ContextPool.AllocateOperationContext(out JsonOperationContext context);

                var getDocumentCommand = new GetDocumentCommand(new[] { "FooBars/1-A", "FooBars/2-A", "FooBars/3-A", "FooBars/4-A" }, includes: null, transformer: null,
                    transformerParameters: null, metadataOnly: false, context: context);

                store.GetRequestExecutor(store.Database).Execute(getDocumentCommand, context);

                var results = getDocumentCommand.Result.Results;

                Assert.Equal(4, results.Length);

                var doc1 = results[0];
                var doc2 = results[1];
                var doc3 = results[2];
                var doc4 = results[3];
                Assert.NotNull(doc1);
                Assert.NotNull(doc2);
                Assert.NotNull(doc3);
                Assert.NotNull(doc4);

                object name;
                ((BlittableJsonReaderObject)doc1).TryGetMember("Name", out name);
                Assert.Equal("John Doe", name.ToString());
                ((BlittableJsonReaderObject)doc2).TryGetMember("Name", out name);
                Assert.Equal("Jane Doe", name.ToString());
                ((BlittableJsonReaderObject)doc3).TryGetMember("Name", out name);
                Assert.Equal("Mega John", name.ToString());
                ((BlittableJsonReaderObject)doc4).TryGetMember("Name", out name);
                Assert.Equal("Mega Jane", name.ToString());
            }
        }

        public class FooBarIndex : AbstractIndexCreationTask<FooBar>
        {
            public FooBarIndex()
            {
                Map = foos => foos.Select(x => new { x.Name });
            }
        }

        public class FooBar : IEquatable<FooBar>
        {
            public string Name { get; set; }

            public bool Equals(FooBar other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(Name, other.Name);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((FooBar)obj);
            }

            public override int GetHashCode()
            {
                return Name?.GetHashCode() ?? 0;
            }

            public static bool operator ==(FooBar left, FooBar right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(FooBar left, FooBar right)
            {
                return !Equals(left, right);
            }
        }
    }
}
