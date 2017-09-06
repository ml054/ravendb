﻿using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Xunit;

namespace SlowTests.Verifications
{
    public class ReduceAndProject : RavenTestBase
    {
        private class Personnel
        {
            public string Id { get; set; }
            public string LastName { get; set; }
        }

        private class PersonnelRole
        {
            public string Id { get; set; }
            public string PersonnelId { get; set; }
            public string RoleId { get; set; }
        }

        private class PersonnelAll
               : AbstractMultiMapIndexCreationTask<PersonnelAll.Mapping>
        {
            public class Mapping
            {
                public string Id { get; set; }
                public string LastName { get; set; }
                public IEnumerable<string> Roles { get; set; }
            }

            public PersonnelAll()
            {

                AddMap<Personnel>(personnel =>
                    from person in personnel
                    select new Mapping
                    {
                        Id = person.Id,
                        LastName = person.LastName,
                        Roles = null
                    });

                AddMap<PersonnelRole>(roles =>
                    from role in roles
                    select new Mapping
                    {
                        Id = role.PersonnelId,
                        LastName = null,
                        Roles = new string[] { role.RoleId }
                    });

                Reduce = results => from result in results
                                    group result by result.Id
                                        into g
                                    select new Mapping
                                    {
                                        Id = g.Select(a => a.Id).FirstOrDefault(a => a != null),
                                        LastName = g.Select(a => a.LastName).FirstOrDefault(a => a != null),
                                        Roles = g.SelectMany(a => a.Roles)
                                    };
            }
        }

        public class Result
        {
            public string Id { get; set; }
            public string FullName { get; set; }
        }

        [Fact]
        public void WillTransform()
        {
            using (var store = GetDocumentStore())
            {
                var persons = new[] { "Ayende", "Rahien", "Oren", "Enei", "Alias" };
                var roles = new[] { "Administrator", "Programmer", "Support", "Guest", "Someone" };

                foreach (var person in persons)
                {
                    using (var session = store.OpenSession())
                    {
                        var personnel = new Personnel() { LastName = person };
                        session.Store(personnel);

                        foreach (var role in roles)
                        {
                            var roleToStore = new PersonnelRole() { PersonnelId = personnel.Id, RoleId = string.Format("Roles/{0}", role) };
                            session.Store(roleToStore);
                        }
                        session.SaveChanges();
                    }
                }

                new PersonnelAll().Execute(store);

                using (var session = store.OpenSession())
                {
                    // -- Dirty wait for stale
                    session.Query<PersonnelAll.Mapping, PersonnelAll>()
                                         .Customize(customization => customization.WaitForNonStaleResults())
                                         .ToArray();

                    // Without transform (will give 5 results)
                    var results1 = session.Query<PersonnelAll.Mapping, PersonnelAll>().ToArray();

                    // With transform (will give 1 to 4 results, depending on the weather?)
                    var results2 = session.Advanced.DocumentQuery<Result, PersonnelAll>()
                        .RawQuery(@"
from index PersonnelAll
select LastName as FullName, Id
")
                                        .ToArray();
                    Assert.True(results2.All(x=>x.FullName != null));
                    Assert.Equal(results1.Count(), results2.Count());
                }
            }
        }
    }
}
