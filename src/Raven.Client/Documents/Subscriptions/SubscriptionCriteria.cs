using System;
using System.Linq.Expressions;

namespace Raven.Client.Documents.Subscriptions
{
    public class SubscriptionTryout
    {
        public string ChangeVector { get; set; }
        public string Query { get; set; }
    }

    public class SubscriptionCreationOptions
    {
        public string Name { get; set; }
        public string Query{ get; set; }
        public string ChangeVector { get; set; }
        public string MentorNode { get; set; }
    }

    public class SubscriptionCreationOptions<T>
    {
        public string Name { get; set; }
        public Expression<Func<T, bool>> Filter { get; set; }
        public Expression<Func<T, object>> Project { get; set; }
        public string ChangeVector { get; set; }
    }


    public class Revision<T> where T : class
    {
        public T Previous;
        public T Current;
    }
}
