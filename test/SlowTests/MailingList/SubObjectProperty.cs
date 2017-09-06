using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Xunit;

namespace SlowTests.MailingList
{
    public class SubObjectProperty : RavenTestBase
    {
        [Fact]
        public void CanProjectProperly()
        {
            using (var store = GetDocumentStore())
            {
                new Sites_ByAdminData().Execute(store);
                using (var session = store.OpenSession())
                {
                    session.Store(new SiteModel
                    {
                        Admin =
                            {
                                GroupName = "Users"
                            }
                    });

                    session.Store(new SiteModel
                    {
                        Admin =
                        {
                            GroupName = "Admins"
                        }
                    });

                    session.Store(new SiteModel
                    {
                        Admin =
                        {
                            GroupName = "Admins"
                        }
                    });

                    session.SaveChanges();
                }

                using (var session = store.OpenSession())
                {
                    var groups = session.Query<SiteModel, Sites_ByAdminData>()
                        .Customize(x => x.WaitForNonStaleResults())
                        .OrderBy(x => x.Admin.GroupName)
                        .Select(s => s.Admin.GroupName)
                        .Distinct()
                        .ToArray();

                    Assert.Equal(new[] { "Admins", "Users" }, groups);
                }
            }
        }

        private class SiteModel
        {
            public string Id { get; set; }
            public AdminData Admin { get; set; }

            // Lots of other crap!

            public SiteModel()
            {
                Admin = new AdminData();
            }
        }

        private class AdminData
        {
            public string Name { get; set; }
            public string GroupName { get; set; }
            public SiteStatus Status { get; set; }
        }

        private enum SiteStatus
        {
            None
        }

        private class Sites_ByAdminData : AbstractIndexCreationTask<SiteModel>
        {
            public Sites_ByAdminData()
            {
                Map = sites => from site in sites
                               select new
                               {
                                   site.Id,
                                   Admin_GroupName = site.Admin.GroupName,
                                   Admin_Name = site.Admin.Name,
                                   Admin_Status = site.Admin.Status
                               };

                Store(s => s.Admin.GroupName, FieldStorage.Yes);
                Store(s => s.Admin.Name, FieldStorage.Yes);
                Store(s => s.Admin.Status, FieldStorage.Yes);

                Index(s => s.Admin.GroupName, FieldIndexing.Search);
                Index(s => s.Admin.Name, FieldIndexing.Search);
                Index(s => s.Admin.Status, FieldIndexing.Exact);
            }
        }
    }
}
