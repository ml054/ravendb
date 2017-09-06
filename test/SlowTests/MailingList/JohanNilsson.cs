using System.Reflection;
using FastTests;
using Xunit;
using System.Linq;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Linq;

namespace SlowTests.MailingList
{
    public class JohanNilsson : RavenTestBase
    {
        private interface IEntity
        {
            string Id2 { get; set; }
        }

        private interface IDomainObject : IEntity
        {
            string ImportantProperty { get; }
        }

        private class DomainObject : IDomainObject
        {
            public string Id2 { get; set; }
            public string ImportantProperty { get; set; }
        }

        [Fact(Skip = "RavenDB-6124")]
        public void WithCustomizedTagNameAndIdentityProperty()
        {
            var id = string.Empty;
            using (var store = GetDocumentStore())
            {
                var defaultFindIdentityProperty = store.Conventions.FindIdentityProperty;
                store.Conventions.FindIdentityProperty = property =>
                    typeof(IEntity).GetTypeInfo().IsAssignableFrom(property.DeclaringType)
                      ? property.Name == "Id2"
                      : defaultFindIdentityProperty(property);

                store.Conventions.FindCollectionName = type =>
                                                    typeof(IDomainObject).IsAssignableFrom(type)
                                                        ? "domainobjects"
                                                        : DocumentConventions.DefaultGetCollectionName(type);

                using (var session = store.OpenSession())
                {
                    var domainObject = new DomainObject();
                    session.Store(domainObject);
                    var domainObject2 = new DomainObject();
                    session.Store(domainObject2);
                    session.SaveChanges();
                    id = domainObject.Id2;
                }
                var matchingDomainObjects = store.OpenSession().Query<IDomainObject>().Where(_ => _.Id2 == id).ToList();
                Assert.Equal(matchingDomainObjects.Count, 1);
            }
        }
    }
}
