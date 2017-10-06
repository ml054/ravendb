using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session.Operations.Lazy;
using Raven.Client.Documents.Session.Tokens;
using Raven.Client.Util;
using Sparrow.Json;

namespace Raven.Client.Documents.Session
{
    /// <summary>
    /// A query against a Raven index
    /// </summary>
    public partial class DocumentQuery<T> : AbstractDocumentQuery<T, DocumentQuery<T>>, IDocumentQuery<T>, IRawDocumentQuery<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentQuery{T}"/> class.
        /// </summary>
        public DocumentQuery(InMemoryDocumentSessionOperations session, string indexName, string collectionName, bool isGroupBy, string fromAlias = null)
            : base(session, indexName, collectionName, isGroupBy, fromAlias)
        {
        }

        /// <inheritdoc />
        public IDocumentQuery<TProjection> SelectFields<TProjection>()
        {
            var propertyInfos = ReflectionUtil.GetPropertiesAndFieldsFor<TProjection>(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ToList();
            var projections = propertyInfos.Select(x => x.Name).ToArray();
            var identityProperty = Conventions.GetIdentityProperty(typeof(TProjection));
            var fields = propertyInfos.Select(p => p == identityProperty ? Constants.Documents.Indexing.Fields.DocumentIdFieldName : p.Name).ToArray();
            return SelectFields<TProjection>(fields, projections);
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Distinct()
        {
            Distinct();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.OrderByScore()
        {
            OrderByScore();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.OrderByScoreDescending()
        {
            OrderByScoreDescending();
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> ExplainScores()
        {
            ShouldExplainScores = true;
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<TProjection> SelectFields<TProjection>(params string[] fields)
        {
            return SelectFields<TProjection>(fields, fields);
        }

        /// <inheritdoc />
        public IDocumentQuery<TProjection> SelectFields<TProjection>(string[] fields, string[] projections, string fromAlias = null)
        {
            return CreateDocumentQueryInternal<TProjection>(fields.Length > 0 ? FieldsToFetchToken.Create(fields, projections, fromAlias != null) : null, fromAlias);
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.WaitForNonStaleResults(TimeSpan waitTimeout)
        {
            WaitForNonStaleResults(waitTimeout);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.WaitForNonStaleResults(TimeSpan waitTimeout)
        {
            WaitForNonStaleResults(waitTimeout);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.AddOrder(string fieldName, bool descending, OrderingType ordering)
        {
            if (descending)
                OrderByDescending(fieldName, ordering);
            else
                OrderBy(fieldName, ordering);

            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> AddOrder<TValue>(Expression<Func<T, TValue>> propertySelector, bool descending, OrderingType ordering)
        {
            var fieldName = GetMemberQueryPath(propertySelector.Body);
            if (descending)
                OrderByDescending(fieldName, ordering);
            else
                OrderBy(fieldName, ordering);

            return this;
        }

        void IQueryBase<T, IDocumentQuery<T>>.AfterQueryExecuted(Action<QueryResult> action)
        {
            AfterQueryExecuted(action);
        }

        void IQueryBase<T, IRawDocumentQuery<T>>.AfterQueryExecuted(Action<QueryResult> action)
        {
            AfterQueryExecuted(action);
        }

        void IQueryBase<T, IDocumentQuery<T>>.AfterStreamExecuted(Action<BlittableJsonReaderObject> action)
        {
            AfterStreamExecuted(action);
        }

        void IQueryBase<T, IRawDocumentQuery<T>>.AfterStreamExecuted(Action<BlittableJsonReaderObject> action)
        {
            AfterStreamExecuted(action);
        }
        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.OpenSubclause()
        {
            OpenSubclause();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.CloseSubclause()
        {
            CloseSubclause();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Search(string fieldName, string searchTerms, SearchOperator @operator)
        {
            Search(fieldName, searchTerms, @operator);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> Search<TValue>(Expression<Func<T, TValue>> propertySelector, string searchTerms, SearchOperator @operator)
        {
            Search(GetMemberQueryPath(propertySelector.Body), searchTerms, @operator);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Intersect()
        {
            Intersect();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.ContainsAny(string fieldName, IEnumerable<object> values)
        {
            ContainsAny(fieldName, values);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> ContainsAny<TValue>(Expression<Func<T, TValue>> propertySelector, IEnumerable<TValue> values)
        {
            ContainsAny(GetMemberQueryPath(propertySelector.Body), values.Cast<object>());
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.ContainsAll(string fieldName, IEnumerable<object> values)
        {
            ContainsAll(fieldName, values);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> ContainsAll<TValue>(Expression<Func<T, TValue>> propertySelector, IEnumerable<TValue> values)
        {
            ContainsAll(GetMemberQueryPath(propertySelector.Body), values.Cast<object>());
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.Statistics(out QueryStatistics stats)
        {
            Statistics(out stats);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.Statistics(out QueryStatistics stats)
        {
            Statistics(out stats);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.UsingDefaultOperator(QueryOperator queryOperator)
        {
            UsingDefaultOperator(queryOperator);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.UsingDefaultOperator(QueryOperator queryOperator)
        {
            UsingDefaultOperator(queryOperator);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.NoTracking()
        {
            NoTracking();
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.NoTracking()
        {
            NoTracking();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.NoCaching()
        {
            NoCaching();
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.NoCaching()
        {
            NoCaching();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.ShowTimings()
        {
            ShowTimings();
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.ShowTimings()
        {
            ShowTimings();
            return this;
        }
        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Include(string path)
        {
            Include(path);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Include(Expression<Func<T, object>> path)
        {
            Include(path);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Not
        {
            get
            {
                NegateNext();
                return this;
            }
        }

        /// <inheritdoc />
        public QueryResult QueryResult
        {
            get
            {
                InitSync();

                return QueryOperation.CurrentQueryResults.CreateSnapshot();
            }
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.Take(int count)
        {
            Take(count);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.Take(int count)
        {
            Take(count);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.Skip(int count)
        {
            Skip(count);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.Skip(int count)
        {
            Skip(count);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereLucene(string fieldName, string whereClause)
        {
            WhereLucene(fieldName, whereClause);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereEquals(string fieldName, object value, bool exact)
        {
            WhereEquals(fieldName, value, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereEquals<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value, bool exact)
        {
            WhereEquals(GetMemberQueryPath(propertySelector.Body), value, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereEquals(WhereParams whereParams)
        {
            WhereEquals(whereParams);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereNotEquals(string fieldName, object value, bool exact)
        {
            WhereNotEquals(fieldName, value, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereNotEquals<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value, bool exact)
        {
            WhereNotEquals(GetMemberQueryPath(propertySelector.Body), value, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereNotEquals(WhereParams whereParams)
        {
            WhereNotEquals(whereParams);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereIn(string fieldName, IEnumerable<object> values, bool exact)
        {
            WhereIn(fieldName, values, exact);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereIn<TValue>(Expression<Func<T, TValue>> propertySelector, IEnumerable<TValue> values, bool exact = false)
        {
            WhereIn(GetMemberQueryPath(propertySelector.Body), values.Cast<object>(), exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereStartsWith(string fieldName, object value)
        {
            WhereStartsWith(fieldName, value);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereStartsWith<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value)
        {
            WhereStartsWith(GetMemberQueryPath(propertySelector.Body), value);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereEndsWith(string fieldName, object value)
        {
            WhereEndsWith(fieldName, value);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereEndsWith<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value)
        {
            WhereEndsWith(GetMemberQueryPath(propertySelector.Body), value);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereBetween(string fieldName, object start, object end, bool exact)
        {
            WhereBetween(fieldName, start, end, exact);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereBetween<TValue>(Expression<Func<T, TValue>> propertySelector, TValue start, TValue end, bool exact = false)
        {
            WhereBetween(GetMemberQueryPath(propertySelector.Body), start, end, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereGreaterThan(string fieldName, object value, bool exact)
        {
            WhereGreaterThan(fieldName, value, exact);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereGreaterThan<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value, bool exact = false)
        {
            WhereGreaterThan(GetMemberQueryPath(propertySelector.Body), value, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereGreaterThanOrEqual(string fieldName, object value, bool exact)
        {
            WhereGreaterThanOrEqual(fieldName, value, exact);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereGreaterThanOrEqual<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value, bool exact = false)
        {
            WhereGreaterThanOrEqual(GetMemberQueryPath(propertySelector.Body), value, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereLessThan(string fieldName, object value, bool exact)
        {
            WhereLessThan(fieldName, value, exact);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereLessThan<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value, bool exact = false)
        {
            WhereLessThan(GetMemberQueryPath(propertySelector.Body), value, exact);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereLessThanOrEqual(string fieldName, object value, bool exact)
        {
            WhereLessThanOrEqual(fieldName, value, exact);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereLessThanOrEqual<TValue>(Expression<Func<T, TValue>> propertySelector, TValue value, bool exact = false)
        {
            WhereLessThanOrEqual(GetMemberQueryPath(propertySelector.Body), value, exact);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> WhereExists<TValue>(Expression<Func<T, TValue>> propertySelector)
        {
            WhereExists(GetMemberQueryPath(propertySelector.Body));
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.WhereExists(string fieldName)
        {
            WhereExists(fieldName);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.AndAlso()
        {
            AndAlso();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.OrElse()
        {
            OrElse();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Boost(decimal boost)
        {
            Boost(boost);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Fuzzy(decimal fuzzy)
        {
            Fuzzy(fuzzy);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.Proximity(int proximity)
        {
            Proximity(proximity);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.RandomOrdering()
        {
            RandomOrdering();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.RandomOrdering(string seed)
        {
            RandomOrdering(seed);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.CustomSortUsing(string typeName, bool descending)
        {
            CustomSortUsing(typeName, descending);
            return this;
        }

        /// <inheritdoc />
        IGroupByDocumentQuery<T> IDocumentQuery<T>.GroupBy(string fieldName, params string[] fieldNames)
        {
            GroupBy(fieldName, fieldNames);
            return new GroupByDocumentQuery<T>(this);
        }

        /// <inheritdoc />
        IDocumentQuery<T> IRawDocumentQuery<T>.RawQuery(string query)
        {
            RawQuery(query);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IRawDocumentQuery<T>.AddParameter(string name, object value)
        {
            AddParameter(name, value);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<TResult> OfType<TResult>()
        {
            return CreateDocumentQueryInternal<TResult>();
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.OrderBy(string field, OrderingType ordering)
        {
            OrderBy(field, ordering);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> OrderBy<TValue>(params Expression<Func<T, TValue>>[] propertySelectors)
        {
            foreach (var item in propertySelectors)
            {
                OrderBy(GetMemberQueryPathForOrderBy(item), OrderingUtil.GetOrderingOfType(item.ReturnType));
            }
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IDocumentQueryBase<T, IDocumentQuery<T>>.OrderByDescending(string field, OrderingType ordering)
        {
            OrderByDescending(field, ordering);
            return this;
        }

        /// <inheritdoc />
        public IDocumentQuery<T> OrderByDescending<TValue>(params Expression<Func<T, TValue>>[] propertySelectors)
        {
            foreach (var item in propertySelectors)
            {
                OrderByDescending(GetMemberQueryPathForOrderBy(item), OrderingUtil.GetOrderingOfType(item.ReturnType));
            }

            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.WaitForNonStaleResultsAsOf(long cutOffEtag)
        {
            WaitForNonStaleResultsAsOf(cutOffEtag);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.WaitForNonStaleResultsAsOf(long cutOffEtag)
        {
            WaitForNonStaleResultsAsOf(cutOffEtag);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.WaitForNonStaleResultsAsOf(long cutOffEtag, TimeSpan waitTimeout)
        {
            WaitForNonStaleResultsAsOf(cutOffEtag, waitTimeout);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.WaitForNonStaleResultsAsOf(long cutOffEtag, TimeSpan waitTimeout)
        {
            WaitForNonStaleResultsAsOf(cutOffEtag, waitTimeout);
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.WaitForNonStaleResults()
        {
            WaitForNonStaleResults();
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.WaitForNonStaleResults()
        {
            WaitForNonStaleResults();
            return this;
        }

        /// <inheritdoc />
        IDocumentQuery<T> IQueryBase<T, IDocumentQuery<T>>.BeforeQueryExecuted(Action<IndexQuery> beforeQueryExecuted)
        {
            BeforeQueryExecuted(beforeQueryExecuted);
            return this;
        }

        /// <inheritdoc />
        IRawDocumentQuery<T> IQueryBase<T, IRawDocumentQuery<T>>.BeforeQueryExecuted(Action<IndexQuery> beforeQueryExecuted)
        {
            BeforeQueryExecuted(beforeQueryExecuted);
            return this;
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            InitSync();
            return QueryOperation.Complete<T>().GetEnumerator();
        }

        /// <inheritdoc />
        public T First()
        {
            return ExecuteQueryOperation(1).First();
        }

        /// <inheritdoc />
        public T FirstOrDefault()
        {
            return ExecuteQueryOperation(1).FirstOrDefault();
        }

        /// <inheritdoc />
        public T Single()
        {
            return ExecuteQueryOperation(2).Single();
        }

        /// <inheritdoc />
        public T SingleOrDefault()
        {
            return ExecuteQueryOperation(2).SingleOrDefault();
        }

        private IEnumerable<T> ExecuteQueryOperation(int take)
        {
            if (PageSize.HasValue == false || PageSize > take)
                Take(take);

            InitSync();

            return QueryOperation.Complete<T>();
        }

        /// <inheritdoc />
        public int Count()
        {
            Take(0);
            var queryResult = QueryResult;
            return queryResult.TotalResults;
        }

        /// <inheritdoc />
        public Lazy<IEnumerable<T>> Lazily()
        {
            return Lazily(null);
        }

        /// <inheritdoc />
        public Lazy<int> CountLazily()
        {
            if (QueryOperation == null)
            {
                Take(0);
                QueryOperation = InitializeQueryOperation();
            }


            var lazyQueryOperation = new LazyQueryOperation<T>(TheSession.Conventions, QueryOperation, AfterQueryExecutedCallback);

            return ((DocumentSession)TheSession).AddLazyCountOperation(lazyQueryOperation);
        }

        /// <inheritdoc />
        public Lazy<IEnumerable<T>> Lazily(Action<IEnumerable<T>> onEval)
        {
            if (QueryOperation == null)
            {
                QueryOperation = InitializeQueryOperation();
            }

            var lazyQueryOperation = new LazyQueryOperation<T>(TheSession.Conventions, QueryOperation, AfterQueryExecutedCallback);
            return ((DocumentSession)TheSession).AddLazyOperation(lazyQueryOperation, onEval);
        }

        protected void InitSync()
        {
            if (QueryOperation != null)
                return;

            var beforeQueryExecutedEventArgs = new BeforeQueryExecutedEventArgs(TheSession, this);
            TheSession.OnBeforeQueryExecutedInvoke(beforeQueryExecutedEventArgs);

            QueryOperation = InitializeQueryOperation();
            ExecuteActualQuery();
        }

        private void ExecuteActualQuery()
        {
            using (QueryOperation.EnterQueryContext())
            {
                QueryOperation.LogQuery();
                var command = QueryOperation.CreateRequest();
                TheSession.RequestExecutor.Execute(command, TheSession.Context);
                QueryOperation.SetResult(command.Result);
            }

            InvokeAfterQueryExecuted(QueryOperation.CurrentQueryResults);
        }

        private DocumentQuery<TResult> CreateDocumentQueryInternal<TResult>(FieldsToFetchToken newFieldsToFetch = null, string fromAlias = null)
        {
            if (newFieldsToFetch != null)
                UpdateFieldsToFetchToken(newFieldsToFetch);

            var query = new DocumentQuery<TResult>(
                TheSession,
                IndexName,
                CollectionName,
                IsGroupBy,
                fromAlias)
            {
                QueryRaw = QueryRaw,
                PageSize = PageSize,
                SelectTokens = SelectTokens,
                FieldsToFetchToken = FieldsToFetchToken,
                WhereTokens = WhereTokens,
                OrderByTokens = OrderByTokens,
                GroupByTokens = GroupByTokens,
                QueryParameters = QueryParameters,
                Start = Start,
                Timeout = Timeout,
                CutoffEtag = CutoffEtag,
                QueryStats = QueryStats,
                TheWaitForNonStaleResults = TheWaitForNonStaleResults,
                Negate = Negate,
                Includes = new HashSet<string>(Includes),
                RootTypes = { typeof(T) },
                BeforeQueryExecutedCallback = BeforeQueryExecutedCallback,
                AfterQueryExecutedCallback = AfterQueryExecutedCallback,
                AfterStreamExecutedCallback = AfterStreamExecutedCallback,
                HighlightedFields = new List<HighlightedField>(HighlightedFields),
                HighlighterPreTags = HighlighterPreTags,
                HighlighterPostTags = HighlighterPostTags,
                DisableEntitiesTracking = DisableEntitiesTracking,
                DisableCaching = DisableCaching,
                ShowQueryTimings = ShowQueryTimings,
                LastEquality = LastEquality,
                ShouldExplainScores = ShouldExplainScores,
                IsIntersect = IsIntersect,
                DefaultOperator = DefaultOperator
            };

            query.AfterQueryExecuted(AfterQueryExecutedCallback);
            return query;
        }
    }
}
