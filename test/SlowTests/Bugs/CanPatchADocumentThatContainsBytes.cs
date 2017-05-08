// -----------------------------------------------------------------------
//  <copyright file="CanPatchADocumentThatContainsBytes.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
using Xunit;

namespace SlowTests.Bugs
{
    public class CanPatchADocumentThatContainsBytes : RavenTestBase
    {
        [Fact]
        public void DocumentWithBytes()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new User
                    {
                        EmailEncrypted = new byte[] { 1, 2, 3, 4, 5, 6 },
                        Skills = new Collection<UserSkill>
                        {
                            new UserSkill {SkillId = 1, IsPrimary = true},
                        }
                    });
                    session.SaveChanges();
                }

                new PrimarySkills().Execute(store);
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var index = new IndexQuery()
                    {
                        Query =
                            session.Query<PrimarySkills.Result, PrimarySkills>()
                                .Where(result => result.SkillId == 1)
                                .ToString()
                    };
                    var patch = new PatchRequest
                    {
                        Script = @"
for (var i = 0; i < this.Skills.$values.length; i++) {
    this.Skills.$values[i].IsPrimary = false
}
"
                    };

                    var operation = store.Operations.Send(new PatchByIndexOperation("PrimarySkills", index, patch));

                    operation.WaitForCompletion(TimeSpan.FromSeconds(30));

                    var user = session.Load<User>("Users/1");
                    Assert.False(user.Skills.Single().IsPrimary);
                }
            }
        }

        private class User
        {
            //public int Id { get; set; }
            public byte[] EmailEncrypted { get; set; }
            public ICollection<UserSkill> Skills { get; set; }
        }

        private class UserSkill
        {
            public int SkillId { get; set; }
            public bool IsPrimary { get; set; }
        }

        private class Skill
        {
            public int Id { get; set; }
        }

        private class PrimarySkills : AbstractIndexCreationTask<User, PrimarySkills.Result>
        {

            public class Result
            {
                public int SkillId { get; set; }

                public bool IsPrimary { get; set; }
            }

            public PrimarySkills()
            {
                Map = users => from u in users
                               from s in u.Skills
                               where s.IsPrimary
                               select new
                               {
                                   s.SkillId,
                                   s.IsPrimary
                               };

                Store(x => x.SkillId, FieldStorage.Yes);
                Store(x => x.IsPrimary, FieldStorage.Yes);
            }
        }
    }
}
