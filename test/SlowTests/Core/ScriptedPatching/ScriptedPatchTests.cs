using FastTests;
using Raven.Client.Documents.Exceptions.Patching;
using Raven.Client.Documents.Operations;
using Raven.Server.Config;
using Xunit;

namespace SlowTests.Core.ScriptedPatching
{
    public class ScriptedPatchTests : RavenTestBase
    {
        public class Foo
        {
            public string Id { get; set; }

            public string BarId { get; set; }

            public string FirstName { get; set; }

            public string Fullname { get; set; }
        }

        public class Bar
        {
            public string Id { get; set; }

            public string LastName { get; set; }
        }

        [Fact]
        public void Max_script_steps_can_be_increased_from_inside_script()
        {
            using (var store = GetDocumentStore(modifyDatabaseRecord: document =>
            {
                document.Settings[RavenConfiguration.GetKey(x => x.Patching.MaxStepsForScript)] = "5000";
                document.Settings[RavenConfiguration.GetKey(x => x.Patching.AllowScriptsToAdjustNumberOfSteps)] = "true";
            }))
            {
                var foo = new Foo
                {
                    BarId = "bar/1",
                    FirstName = "Joe"
                };

                using (var session = store.OpenSession())
                {
                    session.Store(new Bar
                    {
                        Id = "bar/1",
                        LastName = "Doe"
                    });

                    session.Store(foo);
                    session.SaveChanges();
                }

                Assert.Throws<JavaScriptException>(() =>
                {
                    store.Operations.Send(new PatchOperation(foo.Id, null, new PatchRequest
                    {
                        Script = @"for(var i = 0;i < 7500;i++){}"
                    }));
                });

                store.Operations.Send(new PatchOperation(foo.Id, null, new PatchRequest
                {
                    Script = @"IncreaseNumberOfAllowedStepsBy(4500); for(var i = 0;i < 7500;i++){}"
                }));
            }
        }

        [Fact]
        public void Load_document_should_increase_max_steps_in_algorithm()
        {
            using (var store = GetDocumentStore(modifyDatabaseRecord: document =>
            {
                document.Settings[RavenConfiguration.GetKey(x => x.Patching.MaxStepsForScript)] = "5000";
                document.Settings[RavenConfiguration.GetKey(x => x.Patching.AllowScriptsToAdjustNumberOfSteps)] = "true";
            }))
            {
                var foo = new Foo
                {
                    BarId = "bar/1",
                    FirstName = "Joe"
                };

                using (var session = store.OpenSession())
                {
                    session.Store(new Bar
                    {
                        Id = "bar/1",
                        LastName = "Doe"
                    });

                    session.Store(foo);
                    session.SaveChanges();
                }

                Assert.Throws<JavaScriptException>(() =>
                {
                    store.Operations.Send(new PatchOperation(foo.Id, null, new PatchRequest
                    {
                        Script = @"for(var i = 0;i < 7500;i++){}"
                    }));
                });

                store.Operations.Send(new PatchOperation(foo.Id, null, new PatchRequest
                {
                    Script = @"LoadDocument('bar/1'); for(var i = 0;i < 7500;i++){}"
                }));
            }
        }
    }
}
