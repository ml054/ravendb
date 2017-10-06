//-----------------------------------------------------------------------
// <copyright file="DocumentQuery.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session.Operations;
using Raven.Client.Documents.Session.Tokens;
using Raven.Client.Extensions;
using Sparrow.Json;

namespace Raven.Client.Documents.Session
{
    /// <summary>
    ///   A query against a Raven index
    /// </summary>
    public abstract partial class AbstractDocumentQuery<T, TSelf> : IDocumentQueryCustomization, IAbstractDocumentQuery<T>
                                                            where TSelf : AbstractDocumentQuery<T, TSelf>
    {
        private readonly Dictionary<string, string> _aliasToGroupByFieldName = new Dictionary<string, string>();

        protected QueryOperator DefaultOperator;

        private readonly LinqPathProvider _linqPathProvider;

        protected readonly HashSet<Type> RootTypes = new HashSet<Type>
        {
            typeof (T)
        };

        private static Dictionary<Type, Func<object, string>> _implicitStringsCache = new Dictionary<Type, Func<object, string>>();

        /// <summary>
        /// Whether to negate the next operation
        /// </summary>
        protected bool Negate;

        /// <summary>
        /// The index to query
        /// </summary>
        public string IndexName { get; }

        public string CollectionName { get; }

        private int _currentClauseDepth;

        protected string QueryRaw;

        protected KeyValuePair<string, object> LastEquality;

        protected Parameters QueryParameters = new Parameters();

        protected bool IsIntersect;

        protected bool IsGroupBy;
        /// <summary>
        /// The session for this query
        /// </summary>
        protected readonly InMemoryDocumentSessionOperations TheSession;

        /// <summary>
        ///   The page size to use when querying the index
        /// </summary>
        protected int? PageSize;

        protected LinkedList<QueryToken> SelectTokens = new LinkedList<QueryToken>();

        protected readonly FromToken FromToken;

        protected internal FieldsToFetchToken FieldsToFetchToken;

        protected LinkedList<QueryToken> WhereTokens = new LinkedList<QueryToken>();

        protected LinkedList<QueryToken> GroupByTokens = new LinkedList<QueryToken>();

        protected LinkedList<QueryToken> OrderByTokens = new LinkedList<QueryToken>();

        /// <summary>
        ///   which record to start reading from
        /// </summary>
        protected int Start;

        private readonly DocumentConventions _conventions;
        /// <summary>
        /// Timeout for this query
        /// </summary>
        protected TimeSpan? Timeout;
        /// <summary>
        /// Should we wait for non stale results
        /// </summary>
        protected bool TheWaitForNonStaleResults;
        /// <summary>
        /// The paths to include when loading the query
        /// </summary>
        protected HashSet<string> Includes = new HashSet<string>();

        /// <summary>
        /// Holds the query stats
        /// </summary>
        protected QueryStatistics QueryStats = new QueryStatistics();

        /// <summary>
        /// Determines if entities should be tracked and kept in memory
        /// </summary>
        protected bool DisableEntitiesTracking;

        /// <summary>
        /// Determine if query results should be cached.
        /// </summary>
        protected bool DisableCaching;

        /// <summary>
        /// Indicates if detailed timings should be calculated for various query parts (Lucene search, loading documents, transforming results). Default: false
        /// </summary>
        protected bool ShowQueryTimings;

        /// <summary>
        /// Determine if scores of query results should be explained
        /// </summary>
        protected bool ShouldExplainScores;

        public bool IsDistinct => SelectTokens.First?.Value is DistinctToken;

        /// <summary>
        /// Gets the document convention from the query session
        /// </summary>
        public DocumentConventions Conventions => _conventions;

        /// <summary>
        ///   Gets the session associated with this document query
        /// </summary>
        public IDocumentSession Session => (IDocumentSession)TheSession;
        public IAsyncDocumentSession AsyncSession => (IAsyncDocumentSession)TheSession;

        public bool IsDynamicMapReduce => GroupByTokens.Count > 0;

        protected long? CutoffEtag;

        private static TimeSpan DefaultTimeout
        {
            get
            {
                if (Debugger.IsAttached) // increase timeout if we are debugging
                    return TimeSpan.FromMinutes(15);

                return TimeSpan.FromSeconds(15);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractDocumentQuery{T, TSelf}"/> class.
        /// </summary>
        protected AbstractDocumentQuery(InMemoryDocumentSessionOperations session,
                                     string indexName,
                                     string collectionName,
                                     bool isGroupBy,
                                     string fromAlias = null)
        {
            IsGroupBy = isGroupBy;
            IndexName = indexName;
            CollectionName = collectionName;

            FromToken = FromToken.Create(indexName, collectionName, fromAlias);

            TheSession = session;
            AfterQueryExecuted(UpdateStatsAndHighlightings);

            _conventions = session == null ? new DocumentConventions() : session.Conventions;
            _linqPathProvider = new LinqPathProvider(_conventions);
        }

        #region TSelf Members

        public void UsingDefaultOperator(QueryOperator @operator)
        {
            if (WhereTokens.Count != 0)
                throw new InvalidOperationException("Default operator can only be set before any where clause is added.");

            DefaultOperator = @operator;
        }

        /// <summary>
        ///   Instruct the query to wait for non stale result for the specified wait timeout.
        /// </summary>
        /// <param name = "waitTimeout">The wait timeout.</param>
        /// <returns></returns>
        public void WaitForNonStaleResults(TimeSpan waitTimeout)
        {
            TheWaitForNonStaleResults = true;
            CutoffEtag = null;
            Timeout = waitTimeout;
        }

        protected internal QueryOperation InitializeQueryOperation()
        {
            var indexQuery = GetIndexQuery();

            return new QueryOperation(TheSession,
                IndexName,
                indexQuery,
                FieldsToFetchToken?.Projections,
                TheWaitForNonStaleResults,
                Timeout,
                DisableEntitiesTracking);
        }

        public IndexQuery GetIndexQuery()
        {
            var query = ToString();
            var indexQuery = GenerateIndexQuery(query);
            BeforeQueryExecutedCallback?.Invoke(indexQuery);

            return indexQuery;
        }

        /// <summary>
        ///   Gets the fields for projection
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetProjectionFields()
        {
            return FieldsToFetchToken?.Projections ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Order the search results randomly
        /// </summary>
        public void RandomOrdering()
        {
            AssertNoRawQuery();
            OrderByTokens.AddLast(OrderByToken.Random);
        }

        /// <summary>
        /// Order the search results randomly using the specified seed
        /// this is useful if you want to have repeatable random queries
        /// </summary>
        public void RandomOrdering(string seed)
        {
            AssertNoRawQuery();
            if (string.IsNullOrWhiteSpace(seed))
            {
                RandomOrdering();
                return;
            }
            OrderByTokens.AddLast(OrderByToken.CreateRandom(seed));
        }

        public void CustomSortUsing(string typeName, bool descending)
        {
            if (descending)
            {
                OrderByDescending(Constants.Documents.Indexing.Fields.CustomSortFieldName + ";" + typeName);
                return;
            }

            OrderBy(Constants.Documents.Indexing.Fields.CustomSortFieldName + ";" + typeName);
        }

        internal void AddGroupByAlias(string fieldName, string projectedName)
        {
            _aliasToGroupByFieldName[projectedName] = fieldName;
        }

        private void AssertNoRawQuery()
        {
            if (QueryRaw != null)
                throw new InvalidOperationException(
                    "RawQuery was called, cannot modify this query by calling on operations that would modify the query (such as Where, Select, OrderBy, GroupBy, etc)");
        }

        public void RawQuery(string query)
        {
            if (SelectTokens.Count != 0 ||
                WhereTokens.Count != 0 ||
                OrderByTokens.Count != 0 ||
                GroupByTokens.Count != 0)
                throw new InvalidOperationException("You can only use RawQuery on a new query, without applying any operations (such as Where, Select, OrderBy, GroupBy, etc)");
            QueryRaw = query;
        }

        public void AddParameter(string name, object value)
        {
            if (QueryParameters.ContainsKey(name))
                throw new InvalidOperationException("The parameter " + name + " was already added");
            QueryParameters[name] = value;
        }

        public void GroupBy(string fieldName, params string[] fieldNames)
        {
            if (FromToken.IsDynamic == false)
                throw new InvalidOperationException("GroupBy only works with dynamic queries.");
            AssertNoRawQuery();
            IsGroupBy = true;

            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            GroupByTokens.AddLast(GroupByToken.Create(fieldName));

            if (fieldNames == null || fieldNames.Length <= 0)
                return;

            foreach (var name in fieldNames)
            {
                fieldName = EnsureValidFieldName(name, isNestedPath: false);

                GroupByTokens.AddLast(GroupByToken.Create(fieldName));
            }
        }

        public void GroupByKey(string fieldName = null, string projectedName = null)
        {
            AssertNoRawQuery();
            IsGroupBy = true;

            if (projectedName != null && _aliasToGroupByFieldName.TryGetValue(projectedName, out var aliasedFieldName))
            {
                if (fieldName == null || fieldName.Equals(projectedName, StringComparison.Ordinal))
                    fieldName = aliasedFieldName;
            }

            SelectTokens.AddLast(GroupByKeyToken.Create(fieldName, projectedName));
        }

        public void GroupBySum(string fieldName, string projectedName = null)
        {
            AssertNoRawQuery();
            IsGroupBy = true;

            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            SelectTokens.AddLast(GroupBySumToken.Create(fieldName, projectedName));
        }

        public void GroupByCount(string projectedName = null)
        {
            AssertNoRawQuery();
            IsGroupBy = true;

            SelectTokens.AddLast(GroupByCountToken.Create(projectedName));
        }

        public void WhereTrue()
        {
            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(null);

            WhereTokens.AddLast(TrueToken.Instance);
        }

        /// <summary>
        ///   Includes the specified path in the query, loading the document specified in that path
        /// </summary>
        /// <param name = "path">The path.</param>
        public void Include(string path)
        {
            Includes.Add(path);
        }

        /// <summary>
        ///   This function exists solely to forbid in memory where clause on IDocumentQuery, because
        ///   that is nearly always a mistake.
        /// </summary>
        [Obsolete(
            @"
You cannot issue an in memory filter - such as Where(x=>x.Name == ""Ayende"") - on IDocumentQuery. 
This is likely a bug, because this will execute the filter in memory, rather than in RavenDB.
Consider using session.Query<T>() instead of session.Advanced.DocumentQuery<T>. The session.Query<T>() method fully supports Linq queries, while session.Advanced.DocumentQuery<T>() is intended for lower level API access.
If you really want to do in memory filtering on the data returned from the query, you can use: session.Advanced.DocumentQuery<T>().ToList().Where(x=>x.Name == ""Ayende"")
"
            , true)]
        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            throw new NotSupportedException();
        }


        /// <summary>
        ///   This function exists solely to forbid in memory where clause on IDocumentQuery, because
        ///   that is nearly always a mistake.
        /// </summary>
        [Obsolete(
            @"
You cannot issue an in memory filter - such as Count(x=>x.Name == ""Ayende"") - on IDocumentQuery. 
This is likely a bug, because this will execute the filter in memory, rather than in RavenDB.
Consider using session.Query<T>() instead of session.Advanced.DocumentQuery<T>. The session.Query<T>() method fully supports Linq queries, while session.Advanced.DocumentQuery<T>() is intended for lower level API access.
If you really want to do in memory filtering on the data returned from the query, you can use: session.Advanced.DocumentQuery<T>().ToList().Count(x=>x.Name == ""Ayende"")
"
            , true)]
        public int Count(Func<T, bool> predicate)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   Includes the specified path in the query, loading the document specified in that path
        /// </summary>
        /// <param name = "path">The path.</param>
        public void Include(Expression<Func<T, object>> path)
        {
            Include(path.ToPropertyPath());
        }

        /// <summary>
        ///   Takes the specified count.
        /// </summary>
        /// <param name = "count">The count.</param>
        /// <returns></returns>
        public void Take(int count)
        {
            PageSize = count;
        }

        /// <summary>
        ///   Skips the specified count.
        /// </summary>
        /// <param name = "count">The count.</param>
        /// <returns></returns>
        public void Skip(int count)
        {
            Start = count;
        }

        /// <summary>
        ///   Filter the results from the index using the specified where clause.
        /// </summary>
        public void WhereLucene(string fieldName, string whereClause)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.Lucene(fieldName, AddQueryParameter(whereClause)));
        }

        /// <summary>
        ///   Simplified method for opening a new clause within the query
        /// </summary>
        /// <returns></returns>
        public void OpenSubclause()
        {
            _currentClauseDepth++;

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(null);

            WhereTokens.AddLast(OpenSubclauseToken.Instance);
        }

        /// <summary>
        ///   Simplified method for closing a clause within the query
        /// </summary>
        /// <returns></returns>
        public void CloseSubclause()
        {
            _currentClauseDepth--;

            WhereTokens.AddLast(CloseSubclauseToken.Instance);
        }

        /// <summary>
        ///   Matches value
        /// </summary>
        public void WhereEquals(string fieldName, object value, bool exact = false)
        {
            WhereEquals(new WhereParams
            {
                FieldName = fieldName,
                Value = value,
                Exact = exact
            });
        }

        /// <summary>
        ///   Matches value
        /// </summary>
        public void WhereEquals(WhereParams whereParams)
        {
            if (Negate)
            {
                Negate = false;
                WhereNotEquals(whereParams);
                return;
            }

            var transformToEqualValue = TransformValue(whereParams);
            LastEquality = new KeyValuePair<string, object>(whereParams.FieldName, transformToEqualValue);

            AppendOperatorIfNeeded(WhereTokens);

            whereParams.FieldName = EnsureValidFieldName(whereParams.FieldName, whereParams.IsNestedPath);
            WhereTokens.AddLast(WhereToken.Equals(whereParams.FieldName, AddQueryParameter(transformToEqualValue), whereParams.Exact));
        }

        /// <summary>
        ///   Not matches value
        /// </summary>
        public void WhereNotEquals(string fieldName, object value, bool exact = false)
        {
            WhereNotEquals(new WhereParams
            {
                FieldName = fieldName,
                Value = value,
                Exact = exact
            });
        }

        /// <summary>
        ///   Not matches value
        /// </summary>
        public void WhereNotEquals(WhereParams whereParams)
        {
            if (Negate)
            {
                Negate = false;
                WhereEquals(whereParams);
                return;
            }

            var transformToEqualValue = TransformValue(whereParams);
            LastEquality = new KeyValuePair<string, object>(whereParams.FieldName, transformToEqualValue);

            AppendOperatorIfNeeded(WhereTokens);

            whereParams.FieldName = EnsureValidFieldName(whereParams.FieldName, whereParams.IsNestedPath);
            WhereTokens.AddLast(WhereToken.NotEquals(whereParams.FieldName, AddQueryParameter(transformToEqualValue), whereParams.Exact));
        }

        ///<summary>
        /// Negate the next operation
        ///</summary>
        public void NegateNext()
        {
            Negate = !Negate;
        }

        /// <summary>
        /// Check that the field has one of the specified value
        /// </summary>
        public void WhereIn(string fieldName, IEnumerable<object> values, bool exact = false)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.In(fieldName, AddQueryParameter(TransformEnumerable(fieldName, UnpackEnumerable(values)).ToArray()), exact));
        }

        /// <summary>
        ///   Matches fields which starts with the specified value.
        /// </summary>
        /// <param name = "fieldName">Name of the field.</param>
        /// <param name = "value">The value.</param>
        public void WhereStartsWith(string fieldName, object value)
        {
            var whereParams = new WhereParams
            {
                FieldName = fieldName,
                Value = value,
                AllowWildcards = true
            };

            var transformToEqualValue = TransformValue(whereParams);
            LastEquality = new KeyValuePair<string, object>(whereParams.FieldName, transformToEqualValue);

            AppendOperatorIfNeeded(WhereTokens);

            whereParams.FieldName = EnsureValidFieldName(whereParams.FieldName, whereParams.IsNestedPath);
            NegateIfNeeded(whereParams.FieldName);

            WhereTokens.AddLast(WhereToken.StartsWith(whereParams.FieldName, AddQueryParameter(transformToEqualValue)));
        }

        /// <summary>
        ///   Matches fields which ends with the specified value.
        /// </summary>
        /// <param name = "fieldName">Name of the field.</param>
        /// <param name = "value">The value.</param>
        public void WhereEndsWith(string fieldName, object value)
        {
            var whereParams = new WhereParams
            {
                FieldName = fieldName,
                Value = value,
                AllowWildcards = true
            };

            var transformToEqualValue = TransformValue(whereParams);
            LastEquality = new KeyValuePair<string, object>(whereParams.FieldName, transformToEqualValue);

            AppendOperatorIfNeeded(WhereTokens);

            whereParams.FieldName = EnsureValidFieldName(whereParams.FieldName, whereParams.IsNestedPath);
            NegateIfNeeded(whereParams.FieldName);

            WhereTokens.AddLast(WhereToken.EndsWith(whereParams.FieldName, AddQueryParameter(transformToEqualValue)));
        }

        /// <summary>
        ///   Matches fields where the value is between the specified start and end, exclusive
        /// </summary>
        /// <param name = "fieldName">Name of the field.</param>
        /// <param name = "start">The start.</param>
        /// <param name = "end">The end.</param>
        /// <returns></returns>
        public void WhereBetween(string fieldName, object start, object end, bool exact = false)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            var fromParameterName = AddQueryParameter(start == null ? "*" : TransformValue(new WhereParams { Value = start, FieldName = fieldName }, forRange: true));
            var toParameterName = AddQueryParameter(end == null ? "NULL" : TransformValue(new WhereParams { Value = end, FieldName = fieldName }, forRange: true));

            WhereTokens.AddLast(WhereToken.Between(fieldName, fromParameterName, toParameterName, exact));
        }

        /// <summary>
        ///   Matches fields where the value is greater than the specified value
        /// </summary>
        /// <param name = "fieldName">Name of the field.</param>
        /// <param name = "value">The value.</param>
        public void WhereGreaterThan(string fieldName, object value, bool exact = false)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.GreaterThan(fieldName, AddQueryParameter(value == null ? "*" : TransformValue(new WhereParams { Value = value, FieldName = fieldName }, forRange: true)), exact));
        }

        /// <summary>
        ///   Matches fields where the value is greater than or equal to the specified value
        /// </summary>
        /// <param name = "fieldName">Name of the field.</param>
        /// <param name = "value">The value.</param>
        public void WhereGreaterThanOrEqual(string fieldName, object value, bool exact = false)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.GreaterThanOrEqual(fieldName, AddQueryParameter(value == null ? "*" : TransformValue(new WhereParams { Value = value, FieldName = fieldName }, forRange: true)), exact));
        }

        /// <summary>
        ///   Matches fields where the value is less than the specified value
        /// </summary>
        /// <param name = "fieldName">Name of the field.</param>
        /// <param name = "value">The value.</param>
        public void WhereLessThan(string fieldName, object value, bool exact = false)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.LessThan(fieldName, AddQueryParameter(value == null ? "NULL" : TransformValue(new WhereParams { Value = value, FieldName = fieldName }, forRange: true)), exact));
        }

        /// <summary>
        ///   Matches fields where the value is less than or equal to the specified value
        /// </summary>
        /// <param name = "fieldName">Name of the field.</param>
        /// <param name = "value">The value.</param>
        public void WhereLessThanOrEqual(string fieldName, object value, bool exact = false)
        {
            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.LessThanOrEqual(fieldName, AddQueryParameter(value == null ? "NULL" : TransformValue(new WhereParams { Value = value, FieldName = fieldName }, forRange: true)), exact));
        }

        /// <summary>
        ///   Add an AND to the query
        /// </summary>
        public void AndAlso()
        {
            if (WhereTokens.Last == null)
                return;

            if (WhereTokens.Last.Value is QueryOperatorToken)
                throw new InvalidOperationException("Cannot add AND, previous token was already an operator token.");

            WhereTokens.AddLast(QueryOperatorToken.And);
        }

        /// <summary>
        ///   Add an OR to the query
        /// </summary>
        public void OrElse()
        {
            if (WhereTokens.Last == null)
                return;

            if (WhereTokens.Last.Value is QueryOperatorToken)
                throw new InvalidOperationException("Cannot add OR, previous token was already an operator token.");

            WhereTokens.AddLast(QueryOperatorToken.Or);
        }

        /// <summary>
        ///   Specifies a boost weight to the last where clause.
        ///   The higher the boost factor, the more relevant the term will be.
        /// </summary>
        /// <param name = "boost">boosting factor where 1.0 is default, less than 1.0 is lower weight, greater than 1.0 is higher weight</param>
        /// <returns></returns>
        /// <remarks>
        ///   http://lucene.apache.org/java/2_4_0/queryparsersyntax.html#Boosting%20a%20Term
        /// </remarks>
        public void Boost(decimal boost)
        {
            if (boost == 1m) // 1.0 is the default
                return;

            var whereToken = WhereTokens.Last?.Value as WhereToken;
            if (whereToken == null)
                throw new InvalidOperationException("Missing where clause");

            if (boost <= 0m)
                throw new ArgumentOutOfRangeException(nameof(boost), "Boost factor must be a positive number");

            whereToken.Boost = boost;
        }

        /// <summary>
        ///   Specifies a fuzziness factor to the single word term in the last where clause
        /// </summary>
        /// <param name = "fuzzy">0.0 to 1.0 where 1.0 means closer match</param>
        /// <returns></returns>
        /// <remarks>
        ///   http://lucene.apache.org/java/2_4_0/queryparsersyntax.html#Fuzzy%20Searches
        /// </remarks>
        public void Fuzzy(decimal fuzzy)
        {
            var whereToken = WhereTokens.Last?.Value as WhereToken;
            if (whereToken == null)
            {
                throw new InvalidOperationException("Missing where clause");
            }

            if (fuzzy < 0m || fuzzy > 1m)
            {
                throw new ArgumentOutOfRangeException(nameof(fuzzy), "Fuzzy distance must be between 0.0 and 1.0");
            }

            //var ch = QueryText[QueryText.Length - 1]; // TODO [ppekrol]
            //if (ch == '"' || ch == ']')
            //{
            //    // this check is overly simplistic
            //    throw new InvalidOperationException("Fuzzy factor can only modify single word terms");
            //}

            whereToken.Fuzzy = fuzzy;
        }

        /// <summary>
        ///   Specifies a proximity distance for the phrase in the last where clause
        /// </summary>
        /// <param name = "proximity">number of words within</param>
        /// <returns></returns>
        /// <remarks>
        ///   http://lucene.apache.org/java/2_4_0/queryparsersyntax.html#Proximity%20Searches
        /// </remarks>
        public void Proximity(int proximity)
        {
            var whereToken = WhereTokens.Last?.Value as WhereToken;
            if (whereToken == null)
            {
                throw new InvalidOperationException("Missing where clause");
            }

            if (proximity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(proximity), "Proximity distance must be a positive number");
            }

            //if (QueryText[QueryText.Length - 1] != '"') // TODO [ppekrol]
            //{
            //    // this check is overly simplistic
            //    throw new InvalidOperationException("Proximity distance can only modify a phrase");
            //}

            whereToken.Proximity = proximity;
        }

        /// <summary>
        ///   Order the results by the specified fields
        ///   The fields are the names of the fields to sort, defaulting to sorting by ascending.
        ///   You can prefix a field name with '-' to indicate sorting by descending or '+' to sort by ascending
        /// </summary>
        public void OrderBy(string field, OrderingType ordering = OrderingType.String)
        {
            AssertNoRawQuery();
            var f = EnsureValidFieldName(field, isNestedPath: false);
            OrderByTokens.AddLast(OrderByToken.CreateAscending(f, ordering));
        }

        /// <summary>
        ///   Order the results by the specified fields
        ///   The fields are the names of the fields to sort, defaulting to sorting by descending.
        ///   You can prefix a field name with '-' to indicate sorting by descending or '+' to sort by ascending
        /// </summary>
        /// <param name = "fields">The fields.</param>
        public void OrderByDescending(string field, OrderingType ordering = OrderingType.String)
        {
            AssertNoRawQuery();
            var f = EnsureValidFieldName(field, isNestedPath: false);
            OrderByTokens.AddLast(OrderByToken.CreateDescending(f, ordering));
        }

        public void OrderByScore()
        {
            AssertNoRawQuery();
            OrderByTokens.AddLast(OrderByToken.ScoreAscending);
        }

        public void OrderByScoreDescending()
        {
            AssertNoRawQuery();
            OrderByTokens.AddLast(OrderByToken.ScoreDescending);
        }

        /// <summary>
        /// Instructs the query to wait for non stale results as of the cutoff etag.
        /// </summary>
        public void WaitForNonStaleResultsAsOf(long cutOffEtag)
        {
            WaitForNonStaleResultsAsOf(cutOffEtag, DefaultTimeout);
        }

        /// <summary>
        /// Instructs the query to wait for non stale results as of the cutoff etag.
        /// </summary>
        public void WaitForNonStaleResultsAsOf(long cutOffEtag, TimeSpan waitTimeout)
        {
            TheWaitForNonStaleResults = true;
            Timeout = waitTimeout;
            CutoffEtag = cutOffEtag;
        }

        /// <summary>
        ///   EXPERT ONLY: Instructs the query to wait for non stale results.
        ///   This shouldn't be used outside of unit tests unless you are well aware of the implications
        /// </summary>
        public void WaitForNonStaleResults()
        {
            WaitForNonStaleResults(DefaultTimeout);
        }

        /// <summary>
        /// Provide statistics about the query, such as total count of matching records
        /// </summary>
        public void Statistics(out QueryStatistics stats)
        {
            stats = QueryStats;
        }

        /// <summary>
        /// Called externally to raise the after query executed callback
        /// </summary>
        public void InvokeAfterQueryExecuted(QueryResult result)
        {
            AfterQueryExecutedCallback?.Invoke(result);
        }

        /// <summary>
        /// Called externally to raise the after stream executed callback
        /// </summary>
        public void InvokeAfterStreamExecuted(BlittableJsonReaderObject result)
        {
            AfterStreamExecutedCallback?.Invoke(result);
        }

        #endregion

        /// <summary>
        ///   Generates the index query.
        /// </summary>
        /// <param name = "query">The query.</param>
        /// <returns></returns>
        protected IndexQuery GenerateIndexQuery(string query)
        {
            var indexQuery = new IndexQuery
            {
                Query = query,
                Start = Start,
                CutoffEtag = CutoffEtag,
                WaitForNonStaleResults = TheWaitForNonStaleResults,
                WaitForNonStaleResultsTimeout = Timeout,
                QueryParameters = QueryParameters,
                DisableCaching = DisableCaching,
                ShowTimings = ShowQueryTimings,
                ExplainScores = ShouldExplainScores
            };

            if (PageSize != null)
                indexQuery.PageSize = PageSize.Value;

            return indexQuery;
        }

        /// <summary>
        /// Perform a search for documents which fields that match the searchTerms.
        /// If there is more than a single term, each of them will be checked independently.
        /// </summary>
        public void Search(string fieldName, string searchTerms, SearchOperator @operator = SearchOperator.Or)
        {
            var hasWhiteSpace = searchTerms.Any(char.IsWhiteSpace);
            LastEquality = new KeyValuePair<string, object>(fieldName,
                hasWhiteSpace ? "(" + searchTerms + ")" : searchTerms
            );

            AppendOperatorIfNeeded(WhereTokens);

            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.Search(fieldName, AddQueryParameter(searchTerms), @operator));
        }


        /// <inheritdoc />
        public override string ToString()
        {
            if (QueryRaw != null)
                return QueryRaw;

            if (_currentClauseDepth != 0)
                throw new InvalidOperationException(string.Format("A clause was not closed correctly within this query, current clause depth = {0}", _currentClauseDepth));

            var queryText = new StringBuilder();

            BuildFrom(queryText);
            BuildGroupBy(queryText);
            BuildWhere(queryText);
            BuildOrderBy(queryText);
            BuildSelect(queryText);
            BuildInclude(queryText);

            return queryText.ToString();
        }

        private void BuildInclude(StringBuilder queryText)
        {
            if (Includes == null || Includes.Count == 0)
                return;

            queryText.Append(" INCLUDE ");
            bool first = true;
            foreach (var include in Includes)
            {
                if (first == false)
                    queryText.Append(",");
                first = false;
                var requiredQuotes = false;
                for (int i = 0; i < include.Length; i++)
                {
                    var ch = include[i];
                    if (char.IsLetterOrDigit(ch) == false && ch != '_' && ch != '.')
                    {
                        requiredQuotes = true;
                        break;
                    }
                }
                if (requiredQuotes)
                {
                    queryText.Append("'").Append(include.Replace("'", "\\'")).Append("'");
                }
                else
                {
                    queryText.Append(include);
                }
            }
        }

        /// <summary>
        /// The last term that we asked the query to use equals on
        /// </summary>
        public KeyValuePair<string, object> GetLastEqualityTerm(bool isAsync = false)
        {
            return LastEquality;
        }

        public void Intersect()
        {
            var last = WhereTokens.Last?.Value;
            if (last is WhereToken || last is CloseSubclauseToken)
            {
                IsIntersect = true;

                WhereTokens.AddLast(IntersectMarkerToken.Instance);
            }
            else
                throw new InvalidOperationException("Cannot add INTERSECT at this point.");
        }

        public void WhereExists(string fieldName)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            WhereTokens.AddLast(WhereToken.Exists(fieldName));
        }

        public void ContainsAny(string fieldName, IEnumerable<object> values)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            var array = TransformEnumerable(fieldName, UnpackEnumerable(values))
                .ToArray();

            if (array.Length == 0)
            {
                WhereTokens.AddLast(TrueToken.Instance);
                return;
            }

            WhereTokens.AddLast(WhereToken.In(fieldName, AddQueryParameter(array), exact: false));
        }

        public void ContainsAll(string fieldName, IEnumerable<object> values)
        {
            fieldName = EnsureValidFieldName(fieldName, isNestedPath: false);

            AppendOperatorIfNeeded(WhereTokens);
            NegateIfNeeded(fieldName);

            var array = TransformEnumerable(fieldName, UnpackEnumerable(values))
                .ToArray();

            if (array.Length == 0)
            {
                WhereTokens.AddLast(TrueToken.Instance);
                return;
            }

            WhereTokens.AddLast(WhereToken.AllIn(fieldName, AddQueryParameter(array)));
        }

        public void AddRootType(Type type)
        {
            RootTypes.Add(type);
        }

        public string GetMemberQueryPathForOrderBy(Expression expression)
        {
            var memberQueryPath = GetMemberQueryPath(expression);
            return memberQueryPath;
        }

        public string GetMemberQueryPath(Expression expression)
        {
            var result = _linqPathProvider.GetPath(expression);
            result.Path = result.Path.Substring(result.Path.IndexOf('.') + 1);

            if (expression.NodeType == ExpressionType.ArrayLength)
                result.Path += ".Length";

            var propertyName = IndexName == null || FromToken.IsDynamic
                ? _conventions.FindPropertyNameForDynamicIndex(typeof(T), IndexName, "", result.Path)
                : _conventions.FindPropertyNameForIndex(typeof(T), IndexName, "", result.Path);
            return propertyName;
        }

        public void Distinct()
        {
            if (IsDistinct)
                throw new InvalidOperationException("This is already a distinct query.");

            SelectTokens.AddFirst(DistinctToken.Instance);
        }

        private void UpdateStatsAndHighlightings(QueryResult queryResult)
        {
            QueryStats.UpdateQueryStats(queryResult);
            Highlightings.Update(queryResult);
        }

        private void BuildSelect(StringBuilder writer)
        {
            if (SelectTokens.Count == 0)
                return;

            writer
                .Append(" SELECT ");

            var token = SelectTokens.First;
            if (SelectTokens.Count == 1 && token.Value is DistinctToken)
            {
                token.Value.WriteTo(writer);
                writer.Append(" *");

                return;
            }

            while (token != null)
            {
                if (token.Previous != null && token.Previous.Value is DistinctToken == false)
                    writer.Append(",");

                AddSpaceIfNeeded(token.Previous?.Value, token.Value, writer);

                token.Value.WriteTo(writer);

                token = token.Next;
            }
        }

        private void BuildFrom(StringBuilder writer)
        {
            FromToken.WriteTo(writer);
        }

        private void BuildWhere(StringBuilder writer)
        {
            if (WhereTokens.Count == 0)
                return;

            writer
                .Append(" WHERE ");

            if (IsIntersect)
                writer.Append("intersect(");

            var token = WhereTokens.First;
            while (token != null)
            {
                AddSpaceIfNeeded(token.Previous?.Value, token.Value, writer);

                token.Value.WriteTo(writer);

                token = token.Next;
            }

            if (IsIntersect)
                writer.Append(") ");
        }

        private void BuildGroupBy(StringBuilder writer)
        {
            if (GroupByTokens.Count == 0)
                return;

            writer
                .Append(" GROUP BY ");

            var token = GroupByTokens.First;
            while (token != null)
            {
                if (token.Previous != null)
                    writer.Append(", ");

                token.Value.WriteTo(writer);

                token = token.Next;
            }
        }

        private void BuildOrderBy(StringBuilder writer)
        {
            if (OrderByTokens.Count == 0)
                return;

            writer
                .Append(" ORDER BY ");

            var token = OrderByTokens.First;
            while (token != null)
            {
                if (token.Previous != null)
                    writer.Append(", ");

                token.Value.WriteTo(writer);

                token = token.Next;
            }

        }

        private static void AddSpaceIfNeeded(QueryToken previousToken, QueryToken currentToken, StringBuilder writer)
        {
            if (previousToken == null)
                return;

            if (previousToken is OpenSubclauseToken || currentToken is CloseSubclauseToken || currentToken is IntersectMarkerToken)
                return;

            writer.Append(" ");
        }

        private void AppendOperatorIfNeeded(LinkedList<QueryToken> tokens)
        {
            AssertNoRawQuery();

            if (tokens.Count == 0)
                return;

            var lastToken = tokens.Last.Value;

            if (lastToken is WhereToken == false && lastToken is CloseSubclauseToken == false)
                return;

            WhereToken lastWhere = null;

            var current = tokens.Last;
            while (current != null)
            {
                lastWhere = current.Value as WhereToken;

                if (lastWhere != null)
                    break;

                current = current.Previous;
            }

            var token = DefaultOperator == QueryOperator.And ? QueryOperatorToken.And : QueryOperatorToken.Or;

            if (lastWhere?.SearchOperator != null)
                token = QueryOperatorToken.Or; // default to OR operator after search if AND was not specified explicitly

            tokens.AddLast(token);
        }

        private IEnumerable<object> TransformEnumerable(string fieldName, IEnumerable<object> values)
        {
            foreach (var value in values)
            {
                var enumerable = value as IEnumerable;
                if (enumerable != null && value is string == false)
                {
                    foreach (var transformedValue in TransformEnumerable(fieldName, enumerable.Cast<object>()))
                        yield return transformedValue;

                    continue;
                }

                var nestedWhereParams = new WhereParams
                {
                    AllowWildcards = true,
                    FieldName = fieldName,
                    Value = value
                };

                yield return TransformValue(nestedWhereParams);
            }
        }

        private void NegateIfNeeded(string fieldName)
        {
            if (Negate == false)
                return;

            Negate = false;

            if (WhereTokens.Count == 0 || WhereTokens.Last.Value is OpenSubclauseToken)
            {
                if (fieldName != null)
                    WhereExists(fieldName);
                else
                    WhereTrue();

                AndAlso();
            }

            WhereTokens.AddLast(NegateToken.Instance);
        }

        private static IEnumerable<object> UnpackEnumerable(IEnumerable items)
        {
            foreach (var item in items)
            {
                var enumerable = item as IEnumerable;
                if (enumerable != null && item is string == false)
                {
                    foreach (var nested in UnpackEnumerable(enumerable))
                    {
                        yield return nested;
                    }
                }
                else
                {
                    yield return item;
                }
            }
        }

        private string EnsureValidFieldName(string fieldName, bool isNestedPath)
        {
            if (TheSession?.Conventions == null || isNestedPath || IsGroupBy)
                return EscapeIfNecessary(fieldName);

            foreach (var rootType in RootTypes)
            {
                var identityProperty = TheSession.Conventions.GetIdentityProperty(rootType);
                if (identityProperty != null && identityProperty.Name == fieldName)
                {
                    return Constants.Documents.Indexing.Fields.DocumentIdFieldName;
                }
            }

            return EscapeIfNecessary(fieldName);

            string EscapeIfNecessary(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                var escape = false;
                for (var i = 0; i < name.Length; i++)
                {
                    var c = name[i];

                    if (i == 0)
                    {
                        if (char.IsLetter(c) == false && c != '_' && c != '@')
                        {
                            escape = true;
                            break;
                        }
                    }
                    else
                    {
                        if (char.IsLetterOrDigit(c) == false && c != '_' && c != '@' && c != '.' && c != '[' && c != ']')
                        {
                            escape = true;
                            break;
                        }
                    }
                }

                if (escape)
                    return $"'{name}'";

                return name;
            }
        }

        private static Func<object, string> GetImplicitStringConversion(Type type)
        {
            if (type == null)
                return null;

            Func<object, string> value;
            var localStringsCache = _implicitStringsCache;
            if (localStringsCache.TryGetValue(type, out value))
                return value;

            var methodInfo = type.GetMethod("op_Implicit", new[] { type });

            if (methodInfo == null || methodInfo.ReturnType != typeof(string))
            {
                _implicitStringsCache = new Dictionary<Type, Func<object, string>>(localStringsCache)
                {
                    {type, null}
                };
                return null;
            }

            var arg = Expression.Parameter(typeof(object), "self");

            var func = (Func<object, string>)Expression.Lambda(Expression.Call(methodInfo, Expression.Convert(arg, type)), arg).Compile();

            _implicitStringsCache = new Dictionary<Type, Func<object, string>>(localStringsCache)
            {
                {type, func}
            };
            return func;
        }

        private object TransformValue(WhereParams whereParams, bool forRange = false)
        {
            if (whereParams.Value == null)
                return null;
            if (Equals(whereParams.Value, string.Empty))
                return string.Empty;

            var type = whereParams.Value.GetType().GetNonNullableType();

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
                return whereParams.Value;
            if (type == typeof(string))
                return (string)whereParams.Value;
            if (type == typeof(int))
                return whereParams.Value;
            if (type == typeof(long))
                return whereParams.Value;
            if (type == typeof(decimal))
                return whereParams.Value;
            if (type == typeof(double))
                return whereParams.Value;
            if (whereParams.Value is TimeSpan)
            {
                if (forRange)
                    return ((TimeSpan)whereParams.Value).Ticks;

                return whereParams.Value;
            }
            if (whereParams.Value is float)
                return whereParams.Value;
            if (whereParams.Value is string)
                return whereParams.Value;
            if (whereParams.Value is bool)
                return whereParams.Value;
            if (whereParams.Value is Guid)
                return whereParams.Value;
            if (type.GetTypeInfo().IsEnum)
                return whereParams.Value;

            if (whereParams.Value is ValueType)
                return Convert.ToString(whereParams.Value, CultureInfo.InvariantCulture);

            var result = GetImplicitStringConversion(whereParams.Value.GetType());
            if (result != null)
                return result(whereParams.Value);

            if (_conventions.TryConvertValueForQuery(whereParams.FieldName, whereParams.Value, forRange, out var strVal))
                return strVal;

            return whereParams.Value;
        }

        private string AddQueryParameter(object value)
        {
            var parameterName = $"p{QueryParameters.Count.ToInvariantString()}";
            QueryParameters.Add(parameterName, value);
            return parameterName;
        }

        protected void UpdateFieldsToFetchToken(FieldsToFetchToken fieldsToFetch)
        {
            FieldsToFetchToken = fieldsToFetch;

            if (SelectTokens.Count == 0)
            {
                SelectTokens.AddLast(fieldsToFetch);
            }
            else
            {
                var current = SelectTokens.First;
                var replaced = false;

                while (current != null)
                {
                    if (current.Value is FieldsToFetchToken)
                    {
                        current.Value = fieldsToFetch;
                        replaced = true;
                        break;
                    }

                    current = current.Next;
                }

                if (replaced == false)
                    SelectTokens.AddLast(fieldsToFetch);
            }
        }
    }
}
