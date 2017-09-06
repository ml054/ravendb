//-----------------------------------------------------------------------
// <copyright file="LinqExtensions.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries.Facets;
using Raven.Client.Documents.Queries.Spatial;
using Raven.Client.Documents.Queries.Suggestion;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Session.Operations;
using Raven.Client.Documents.Session.Operations.Lazy;
using Raven.Client.Extensions;
using Raven.Client.Util;

namespace Raven.Client.Documents
{
    ///<summary>
    /// Extensions to the linq syntax
    ///</summary>
    public static class LinqExtensions
    {
#if !NETSTANDARD2_0
        private static readonly object Locker = new object();

        private static MethodInfo _includeMethod;

        private static MethodInfo _whereMethod2;

        private static MethodInfo _whereMethod3;

        private static MethodInfo _spatialMethod;

        private static MethodInfo _orderByDistanceMethod;

        private static MethodInfo _orderByDistanceDescendingMethod;

        private static MethodInfo _orderByMethod;

        private static MethodInfo _orderByDescendingMethod;

        private static MethodInfo _thenByMethod;

        private static MethodInfo _thenByDescendingMethod;
#endif

        /// <summary>
        /// Includes the specified path in the query, loading the document specified in that path
        /// </summary>
        /// <typeparam name="TResult">The type of the object that holds the id that you want to include.</typeparam>
        /// <param name="source">The source for querying</param>
        /// <param name="path">The path, which is name of the property that holds the id of the object to include.</param>
        /// <returns></returns>
        public static IRavenQueryable<TResult> Include<TResult>(this IRavenQueryable<TResult> source, Expression<Func<TResult, object>> path)
        {
            return source.Include(path.ToPropertyPath());
        }

        /// <summary>
        /// Includes the specified path in the query, loading the document specified in that path
        /// </summary>
        /// <typeparam name="TResult">The type of the object that holds the id that you want to include.</typeparam>
        /// <typeparam name="TInclude">The type of the object that you want to include.</typeparam>
        /// <param name="source">The source for querying</param>
        /// <param name="path">The path, which is name of the property that holds the id of the object to include.</param>
        /// <returns></returns>
        public static IRavenQueryable<TResult> Include<TResult, TInclude>(this IRavenQueryable<TResult> source, Expression<Func<TResult, object>> path)
        {
            var queryInspector = (IRavenQueryInspector)source;
            var conventions = queryInspector.Session.Conventions;
            var idPrefix = conventions.GetCollectionName(typeof(TInclude));
            if (idPrefix != null)
            {
                idPrefix = conventions.TransformTypeCollectionNameToDocumentIdPrefix(idPrefix);
                idPrefix += conventions.IdentityPartsSeparator;
            }

            var id = path.ToPropertyPath() + "(" + idPrefix + ")";
            return source.Include(id);
        }

        /// <summary>
        /// Includes the specified path in the query, loading the document specified in that path
        /// </summary>
        /// <typeparam name="TResult">The type of the object that holds the id that you want to include.</typeparam>
        /// <param name="source">The source for querying</param>
        /// <param name="path">The path, which is name of the property that holds the id of the object to include.</param>
        /// <returns></returns>
        public static IRavenQueryable<TResult> Include<TResult>(this IRavenQueryable<TResult> source, string path)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            MethodInfo currentMethod = GetIncludeMethod();
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(TResult)), expression, Expression.Constant(path)));
            return (IRavenQueryable<TResult>)queryable;
        }

        /// <summary>
        /// Query the facets results for this query using aggregation
        /// </summary>
        public static DynamicAggregationQuery<T> AggregateBy<T>(this IQueryable<T> queryable, string path, string displayName = null)
        {
            return new DynamicAggregationQuery<T>(queryable, path, displayName);
        }

        /// <summary>
        /// Query the facets results for this query using aggregation
        /// </summary>
        public static DynamicAggregationQuery<T> AggregateBy<T>(this IQueryable<T> queryable, Expression<Func<T, object>> path)
        {
            return new DynamicAggregationQuery<T>(queryable, path);
        }

        /// <summary>
        /// Query the facets results for this query using aggregation with a specific display name
        /// </summary>
        public static DynamicAggregationQuery<T> AggregateBy<T>(this IQueryable<T> queryable, Expression<Func<T, object>> path, string displayName)
        {
            return new DynamicAggregationQuery<T>(queryable, path, displayName);
        }

        /// <summary>
        /// Query the facets results for this query using the specified facet document with the given start and pageSize
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="queryable">The queryable interface for the function to be applied to</param>
        public static FacetedQueryResult ToFacets<T>(this IQueryable<T> queryable, string facetSetupDoc, int start = 0, int? pageSize = null)
        {
            var ravenQueryInspector = (IRavenQueryInspector)queryable;
            return ravenQueryInspector.GetFacets(facetSetupDoc, start, pageSize);
        }

        /// <summary>
        /// Transforms the query to the facet query that will allow you to execute multi faceted search
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">>Paging PageSize. If set, overrides Facet.MaxResults</param>
        ///  <param name="queryable">The queryable interface for the function to be applied to</param>
        public static FacetQuery ToFacetQuery<T>(this IQueryable<T> queryable, string facetSetupDoc, int start = 0, int? pageSize = null)
        {
            var ravenQueryInspector = (IRavenQueryInspector)queryable;
            var q = ravenQueryInspector.GetIndexQuery(false);
            var query = FacetQuery.Create(q, facetSetupDoc, null, start, pageSize, ravenQueryInspector.Session.Conventions);

            return query;
        }

        /// <summary>
        /// Query the facets results for this query using the specified list of facets with the given start and pageSize
        /// </summary>
        /// <param name="facets">List of facets</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        ///  <param name="queryable">The queryable interface for the function to be applied to</param>
        public static FacetedQueryResult ToFacets<T>(this IQueryable<T> queryable, IEnumerable<Facet> facets, int start = 0, int? pageSize = null)
        {
            var facetsList = facets.ToList();

            if (!facetsList.Any())
                throw new ArgumentException("Facets must contain at least one entry", nameof(facets));

            var ravenQueryInspector = (IRavenQueryInspector)queryable;

            return ravenQueryInspector.GetFacets(facetsList, start, pageSize);
        }

        /// <summary>
        /// Query the facets results for this query using the specified list of facets with the given start and pageSize
        /// </summary>
        /// <param name="facets">List of facets</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        ///  <param name="queryable">The queryable interface for the function to be applied to</param>
        public static FacetQuery ToFacetQuery<T>(this IQueryable<T> queryable, IEnumerable<Facet> facets, int start = 0, int? pageSize = null)
        {
            var facetsList = facets.ToList();

            if (facetsList.Any() == false)
                throw new ArgumentException("Facets must contain at least one entry", nameof(facets));

            var ravenQueryInspector = (IRavenQueryInspector)queryable;
            var q = ravenQueryInspector.GetIndexQuery(false);
            var query = FacetQuery.Create(q, null, facetsList, start, pageSize, ravenQueryInspector.Session.Conventions);

            return query;
        }

        /// <summary>
        /// Query the facets results for this query using the specified facet document with the given start and pageSize
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="query">The document query interface for the function to be applied to</param>
        public static FacetedQueryResult ToFacets<T>(this IDocumentQuery<T> query, string facetSetupDoc, int start = 0, int? pageSize = null)
        {
            var documentQuery = (DocumentQuery<T>)query;
            return documentQuery.GetFacets(facetSetupDoc, start, pageSize);
        }

        /// <summary>
        /// Query the facets results for this query using the specified list of facets with the given start and pageSize
        /// </summary>
        /// <param name="facets">List of facets</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="query">The document query interface for the function to be applied to</param>
        public static FacetedQueryResult ToFacets<T>(this IDocumentQuery<T> query, IEnumerable<Facet> facets, int start = 0, int? pageSize = null)
        {
            var facetsList = facets.ToList();

            if (!facetsList.Any())
                throw new ArgumentException("Facets must contain at least one entry", nameof(facets));

            var documentQuery = (DocumentQuery<T>)query;

            return documentQuery.GetFacets(facetsList, start, pageSize);
        }

        /// <summary>
        /// Lazily Query the facets results for this query using the specified facet document with the given start and pageSize
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="queryable">The queryable interface for the function to be applied to</param>
        public static Lazy<FacetedQueryResult> ToFacetsLazy<T>(this IQueryable<T> queryable, string facetSetupDoc, int start = 0, int? pageSize = null)
        {
            var ravenQueryInspector = (IRavenQueryInspector)queryable;
            var q = ravenQueryInspector.GetIndexQuery(isAsync: false);
            var query = FacetQuery.Create(q, facetSetupDoc, null, start, pageSize, ravenQueryInspector.Session.Conventions);
            var lazyOperation = new LazyFacetsOperation(ravenQueryInspector.Session.Conventions, query);

            var documentSession = (DocumentSession)ravenQueryInspector.Session;
            return documentSession.AddLazyOperation<FacetedQueryResult>(lazyOperation, null);
        }



        /// <summary>
        /// LazilyAsync Query the facets results for this query using the specified facet document with the given start and pageSize
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="queryable">The queryable interface for the function to be applied to</param>
        public static Lazy<Task<FacetedQueryResult>> ToFacetsLazyAsync<T>(this IQueryable<T> queryable, string facetSetupDoc, int start = 0, int? pageSize = null)
        {
            var ravenQueryInspector = (IRavenQueryInspector)queryable;
            var q = ravenQueryInspector.GetIndexQuery(true);
            var query = FacetQuery.Create(q, facetSetupDoc, null, start, pageSize, ravenQueryInspector.Session.Conventions);
            var lazyOperation = new LazyFacetsOperation(ravenQueryInspector.Session.Conventions, query);

            var documentSession = (AsyncDocumentSession)ravenQueryInspector.Session;
            return documentSession.AddLazyOperation<FacetedQueryResult>(lazyOperation, null);
        }

        /// <summary>
        /// Lazily Query the facets results for this query using the specified list of facets with the given start and pageSize
        /// </summary>
        public static Lazy<FacetedQueryResult> ToFacetsLazy<T>(this IQueryable<T> queryable, IEnumerable<Facet> facets, int start = 0, int? pageSize = null)
        {
            var facetsList = facets.ToList();

            if (facetsList.Any() == false)
                throw new ArgumentException("Facets must contain at least one entry", nameof(facets));

            var ravenQueryInspector = (IRavenQueryInspector)queryable;
            var q = ravenQueryInspector.GetIndexQuery(isAsync: false);
            var query = FacetQuery.Create(q, null, facetsList, start, pageSize, ravenQueryInspector.Session.Conventions);
            var lazyOperation = new LazyFacetsOperation(ravenQueryInspector.Session.Conventions, query);

            var documentSession = (DocumentSession)ravenQueryInspector.Session;
            return documentSession.AddLazyOperation<FacetedQueryResult>(lazyOperation, null);
        }

        /// <summary>
        /// Lazily Query the facets results for this query using the specified facet document with the given start and pageSize
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="query">The document query interface for the function to be applied to</param>
        public static Lazy<FacetedQueryResult> ToFacetsLazy<T>(this IDocumentQuery<T> query, string facetSetupDoc, int start = 0, int? pageSize = null)
        {
            var indexQuery = query.GetIndexQuery();
            var documentQuery = (DocumentQuery<T>)query;
            var facetQuery = FacetQuery.Create(indexQuery, facetSetupDoc, null, start, pageSize, documentQuery.Conventions);
            var lazyOperation = new LazyFacetsOperation(documentQuery.Conventions, facetQuery);

            var documentSession = (DocumentSession)documentQuery.Session;
            return documentSession.AddLazyOperation<FacetedQueryResult>(lazyOperation, null);
        }

        /// <summary>
        /// Lazily Query the facets results for this query using the specified list of facets with the given start and pageSize
        /// </summary>
        /// <param name="facets">List of facets</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="query">The document query interface for the function to be applied to</param>
        public static Lazy<FacetedQueryResult> ToFacetsLazy<T>(this IDocumentQuery<T> query, IEnumerable<Facet> facets, int start = 0, int? pageSize = null)
        {
            var facetsList = facets.ToList();

            if (facetsList.Any() == false)
                throw new ArgumentException("Facets must contain at least one entry", nameof(facets));

            var indexQuery = query.GetIndexQuery();
            var documentQuery = (DocumentQuery<T>)query;
            var facetQuery = FacetQuery.Create(indexQuery, null, facetsList, start, pageSize, documentQuery.Conventions);
            var lazyOperation = new LazyFacetsOperation(documentQuery.Conventions, facetQuery);

            var documentSession = (DocumentSession)documentQuery.Session;
            return documentSession.AddLazyOperation<FacetedQueryResult>(lazyOperation, null);
        }

        /// <summary>
        /// Async Query the facets results for this query using the specified facet document with the given start and pageSize
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="queryable">The queryable interface for the function to be applied to</param>
        /// <param name="token">The cancellation token</param>
        public static Task<FacetedQueryResult> ToFacetsAsync<T>(this IQueryable<T> queryable, string facetSetupDoc, int start = 0, int? pageSize = null, CancellationToken token = default(CancellationToken))
        {
            var ravenQueryInspector = (IRavenQueryInspector)queryable;
            return ravenQueryInspector.GetFacetsAsync(facetSetupDoc, start, pageSize, token);
        }

        /// <summary>
        /// Async Query the facets results for this query using the specified list of facets with the given start and pageSize
        /// </summary>
        /// <param name="facets">List of facets</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="queryable">The queryable interface for the function to be applied to</param>
        /// <param name="token">The cancellation token</param>
        public static Task<FacetedQueryResult> ToFacetsAsync<T>(this IQueryable<T> queryable, IEnumerable<Facet> facets, int start = 0, int? pageSize = null, CancellationToken token = default(CancellationToken))
        {
            var facetsList = facets.ToList();

            if (!facetsList.Any())
                throw new ArgumentException("Facets must contain at least one entry", nameof(facets));

            var ravenQueryInspector = (IRavenQueryInspector)queryable;

            return ravenQueryInspector.GetFacetsAsync(facetsList, start, pageSize, token);
        }

        /// <summary>
        /// Async Query the facets results for this query using the specified facet document with the given start and pageSize
        /// </summary>
        /// <param name="facetSetupDoc">Name of the FacetSetup document</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="queryable">The queryable interface for the function to be applied to</param>
        /// <param name="token">The cancellation token</param>
        public static Task<FacetedQueryResult> ToFacetsAsync<T>(this IAsyncDocumentQuery<T> queryable, string facetSetupDoc, int start = 0, int? pageSize = null, CancellationToken token = default(CancellationToken))
        {
            return queryable.GetFacetsAsync(facetSetupDoc, start, pageSize, token);
        }

        /// <summary>
        /// Async Query the facets results for this query using the specified list of facets with the given start and pageSize
        /// </summary>
        /// <param name="facets">List of facets</param>
        /// <param name="start">Start index for paging</param>
        /// <param name="pageSize">Paging PageSize. If set, overrides Facet.MaxResults</param>
        /// <param name="queryable">The queryable interface for the function to be applied to</param>
        /// <param name="token">The cancellation token</param>
        public static Task<FacetedQueryResult> ToFacetsAsync<T>(this IAsyncDocumentQuery<T> queryable, IEnumerable<Facet> facets, int start = 0, int? pageSize = null, CancellationToken token = default(CancellationToken))
        {
            var facetsList = facets.ToList();
            return queryable.GetFacetsAsync(facetsList, start, pageSize, token);
        }

        /// <summary>
        /// Project into a different type.
        /// </summary>
        public static IQueryable<TResult> As<TResult>(this IQueryable queryable)
        {
            var ofType = queryable.OfType<TResult>();
            var results = queryable.Provider.CreateQuery<TResult>(ofType.Expression);
            return results;
        }

        /// <summary>
        /// Partition the query so we can intersect different parts of the query
        /// across different index entries.
        /// </summary>
        public static IRavenQueryable<T> Intersect<T>(this IQueryable<T> self)
        {
            var currentMethod = typeof(LinqExtensions).GetMethod("Intersect");
            var expression = ConvertExpressionIfNecessary(self);
            var queryable =
                self.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression));
            return (IRavenQueryable<T>)queryable;
        }

        /// <summary>
        /// Project from index fields (must be stored) into different type. If fields are not stored in index, document fields will be used.
        /// </summary>
        public static IRavenQueryable<TResult> ProjectFromIndexFieldsInto<TResult>(this IQueryable queryable)
        {
            var ofType = queryable.OfType<TResult>();
            var results = queryable.Provider.CreateQuery<TResult>(ofType.Expression);
            var ravenQueryInspector = (RavenQueryInspector<TResult>)results;

            var membersList = ReflectionUtil.GetPropertiesAndFieldsFor<TResult>(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();
            ravenQueryInspector.FieldsToFetch(membersList.Select(x => x.Name));
            return (IRavenQueryable<TResult>)results;
        }

        /// <summary>
        /// Suggest alternative values for the queried term
        /// </summary>
        public static SuggestionQueryResult Suggest(this IQueryable queryable)
        {
            return Suggest(queryable, new SuggestionQuery());
        }

        /// <summary>
        /// Suggest alternative values for the queried term
        /// </summary>
        public static SuggestionQueryResult Suggest(this IQueryable source, SuggestionQuery query)
        {
            var inspector = source as IRavenQueryInspector;
            if (inspector == null)
                throw new ArgumentException("You can only use Raven Queryable with suggests");

            SetSuggestionQueryParameters(inspector, query);

            var operation = new SuggestionOperation(inspector.Session, query);

            var command = operation.CreateRequest();
            inspector.Session.RequestExecutor.Execute(command, inspector.Session.Context);

            var result = command.Result;
            operation.SetResult(result);

            return operation.Complete();
        }

        /// <summary>
        /// Lazy Suggest alternative values for the queried term
        /// </summary>
        public static Lazy<SuggestionQueryResult> SuggestLazy(this IQueryable queryable)
        {
            return SuggestLazy(queryable, new SuggestionQuery());
        }

        /// <summary>
        /// Lazy Suggest alternative values for the queried term
        /// </summary>
        public static Lazy<SuggestionQueryResult> SuggestLazy(this IQueryable source, SuggestionQuery query)
        {
            var inspector = source as IRavenQueryInspector;
            if (inspector == null)
                throw new ArgumentException("You can only use Raven Queryable with suggests");

            SetSuggestionQueryParameters(inspector, query);

            var lazyOperation = new LazySuggestionOperation(inspector.Session, query);

            var documentSession = (DocumentSession)inspector.Session;
            return documentSession.AddLazyOperation<SuggestionQueryResult>(lazyOperation, null);
        }

        private static void SetSuggestionQueryParameters(IRavenQueryInspector inspector, SuggestionQuery query, bool isAsync = false)
        {
            query.IndexName = inspector.IndexName;

            if (string.IsNullOrEmpty(query.Field) == false && string.IsNullOrEmpty(query.Term) == false)
                return;

            var lastEqualityTerm = inspector.GetLastEqualityTerm(isAsync);
            if (lastEqualityTerm.Key == null)
                throw new InvalidOperationException("Could not suggest on a query that doesn't have a single equality check");

            query.Field = lastEqualityTerm.Key;
            query.Term = lastEqualityTerm.Value.ToString();
        }

        /// <summary>
        /// Suggest alternative values for the queried term
        /// </summary>
        public static async Task<SuggestionQueryResult> SuggestAsync(this IQueryable source, SuggestionQuery query, CancellationToken token = default(CancellationToken))
        {
            var inspector = source as IRavenQueryInspector;
            if (inspector == null)
                throw new ArgumentException("You can only use Raven Queryable with suggests");

            SetSuggestionQueryParameters(inspector, query, isAsync: true);

            var operation = new SuggestionOperation(inspector.Session, query);

            var command = operation.CreateRequest();
            await inspector.Session.RequestExecutor.ExecuteAsync(command, inspector.Session.Context, token).ConfigureAwait(false);

            var result = command.Result;
            operation.SetResult(result);

            return operation.Complete();
        }

        /// <summary>
        /// Suggest alternative values for the queried term
        /// </summary>
        public static Task<SuggestionQueryResult> SuggestAsync(this IQueryable queryable, CancellationToken token = default(CancellationToken))
        {
            return SuggestAsync(queryable, new SuggestionQuery(), token);
        }

        /// <summary>
        /// LazyAsync Suggest alternative values for the queried term
        /// </summary>
        public static Lazy<Task<SuggestionQueryResult>> SuggestLazyAsync(this IQueryable queryable)
        {
            return SuggestLazyAsync(queryable, new SuggestionQuery());
        }

        /// <summary>
        /// LazyAsync Suggest alternative values for the queried term
        /// </summary>
        public static Lazy<Task<SuggestionQueryResult>> SuggestLazyAsync(this IQueryable source, SuggestionQuery query)
        {
            var inspector = source as IRavenQueryInspector;
            if (inspector == null)
                throw new ArgumentException("You can only use Raven Queryable with suggests");

            SetSuggestionQueryParameters(inspector, query, true);

            var lazyOperation = new LazySuggestionOperation(inspector.Session, query);
            var documentSession = (AsyncDocumentSession)inspector.Session;
            return documentSession.AddLazyOperation<SuggestionQueryResult>(lazyOperation, null);
        }

        /// <summary>
        /// Register the query as a lazy async query in the session and return a lazy async
        /// instance that will evaluate the query only when needed
        /// </summary>
        public static Lazy<Task<IEnumerable<T>>> LazilyAsync<T>(this IQueryable<T> source)
        {
            return LazilyAsync(source, null);
        }

        /// <summary>
        /// Register the query as a lazy async query in the session and return a lazy async
        /// instance that will evaluate the query only when needed
        /// As well as a function to execute when the value is evaluated
        /// </summary>

        public static Lazy<Task<IEnumerable<T>>> LazilyAsync<T>(this IQueryable<T> source, Action<IEnumerable<T>> onEval)
        {
            var provider = source.Provider as IRavenQueryProvider;
            if (provider == null)
                throw new ArgumentException("You can only use Raven Queryable with Lazily");

            return provider.LazilyAsync(source.Expression, onEval);
        }

        /// <summary>
        /// Register the query as a lazy query in the session and return a lazy
        /// instance that will evaluate the query only when needed
        /// </summary>
        public static Lazy<IEnumerable<T>> Lazily<T>(this IQueryable<T> source)
        {
            return Lazily(source, null);
        }

        /// <summary>
        /// Register the query as a lazy query in the session and return a lazy
        /// instance that will evaluate the query only when needed
        /// As well as a function to execute when the value is evaluated
        /// </summary>
        public static Lazy<IEnumerable<T>> Lazily<T>(this IQueryable<T> source, Action<IEnumerable<T>> onEval)
        {
            var provider = source.Provider as IRavenQueryProvider;
            if (provider == null)
                throw new ArgumentException("You can only use Raven Queryable with Lazily");

            return provider.Lazily(source.Expression, onEval);
        }

        /// <summary>
        /// Register the query as a lazy-count query in the session and return a lazy
        /// instance that will evaluate the query only when needed
        /// </summary>
        public static Lazy<int> CountLazily<T>(this IQueryable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("CountLazily only be used with IRavenQueryable");

            return provider.CountLazily<T>(source.Expression);
        }


        /// <summary>
        /// Register the query as a lazy-count query in the session and return a lazy
        /// instance that will evaluate the query only when needed
        /// </summary>
        public static Lazy<Task<int>> CountLazilyAsync<T>(this IQueryable<T> source, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("CountLazily only be used with IRavenQueryable");

            return provider.CountLazilyAsync<T>(source.Expression, token);
        }

        /// <summary>
        /// Returns a list of results for a query asynchronously. 
        /// </summary>
        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken token = default(CancellationToken))
        {
            var provider = source.Provider as IRavenQueryProvider;
            if (provider == null)
                throw new ArgumentException("You can only use Raven Queryable with ToListAsync");

            var documentQuery = provider.ToAsyncDocumentQuery<T>(source.Expression);
            provider.MoveAfterQueryExecuted(documentQuery);
            return documentQuery.ToListAsync(token);
        }

        /// <summary>
        /// Determines whether a sequence contains any elements.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> that contains the elements to be counted.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// true if the source sequence contains any elements; otherwise, false.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source is null.
        /// </exception>
        public static async Task<bool> AnyAsync<TSource>(this IQueryable<TSource> source, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("AnyAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(source.Expression)
                                .Take(0);

            provider.MoveAfterQueryExecuted(query);
            query.Statistics(out var stats);

            await query.ToListAsync(token).ConfigureAwait(false);

            return stats.TotalResults > 0;
        }

        /// <summary>
        /// Determines whether any element of a sequence satisfies a condition.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> that contains the elements to be counted.
        /// </param>
        /// 
        /// <param name="predicate">
        /// A function to test each element for a condition.
        /// </param>
        /// 
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// true if any elements in the source sequence pass the test in the specified
        /// predicate; otherwise, false.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source or predicate is null.
        /// </exception>
        public static async Task<bool> AnyAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var filtered = source.Where(predicate);
            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("AnyAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(filtered.Expression)
                                .Take(0);

            provider.MoveAfterQueryExecuted(query);

            query.Statistics(out var stats);

            await query.ToListAsync(token).ConfigureAwait(false);

            return stats.TotalResults > 0;
        }

        /// <summary>
        /// Returns the number of elements in a sequence.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> that contains the elements to be counted.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The number of elements in the input sequence.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source is null.
        /// </exception>
        /// 
        /// <exception cref="OverflowException">
        /// The number of elements in source is larger than <see cref="Int32.MaxValue"/>.
        /// </exception>
        public static async Task<int> CountAsync<TSource>(this IQueryable<TSource> source, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("CountAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(source.Expression)
                                .Take(0);

            provider.MoveAfterQueryExecuted(query);

            query.Statistics(out var stats);

            await query.ToListAsync(token).ConfigureAwait(false);

            return stats.TotalResults;
        }

        /// <summary>
        /// Returns the number of elements in the specified sequence that satisfies a condition.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> that contains the elements to be counted.
        /// </param>
        /// 
        /// <param name="predicate">
        /// A function to test each element for a condition.
        /// </param>
        /// 
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The number of elements in the sequence that satisfies the condition in
        /// the predicate function.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source or predicate is null.
        /// </exception>
        /// 
        /// <exception cref="OverflowException">
        /// The number of elements in source is larger than <see cref="Int32.MaxValue"/>.
        /// </exception>
        public static async Task<int> CountAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var filtered = source.Where(predicate);
            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("CountAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(filtered.Expression)
                                .Take(0);

            provider.MoveAfterQueryExecuted(query);
            query.Statistics(out var stats);

            await query.ToListAsync(token).ConfigureAwait(false);

            return stats.TotalResults;
        }

        /// <summary>
        /// Asynchronously returns the first element of a sequence.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the first element of.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The first element in source.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// The source sequence is empty or source
        /// is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> FirstAsync<TSource>(this IQueryable<TSource> source, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("FirstAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(source.Expression)
                                .Take(1);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.First();
        }

        /// <summary>
        /// Asynchronously returns the first element of a sequence that satisfies a specified condition.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the first element of.
        /// </param>
        /// 
        /// <param name="predicate">
        /// A function to test each element for a condition.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The first element in source.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source or predicate is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// No element satisfies the condition in predicate,
        /// the source sequence is empty or source
        /// is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> FirstAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var filtered = source.Where(predicate);
            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("FirstAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(filtered.Expression)
                                .Take(1);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.First();
        }

        /// <summary>
        /// Asynchronously returns the first element of a sequence, or a default value if the sequence contains no elements.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the first element of.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// default(TSource) if source is empty; otherwise,
        /// the first element in source.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// source is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> FirstOrDefaultAsync<TSource>(this IQueryable<TSource> source, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("FirstOrDefaultAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(source.Expression)
                                .Take(1);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.FirstOrDefault();
        }

        /// <summary>
        /// Asynchronously returns the first element of a sequence that satisfies a specified
        /// condition or a default value if no such element is found.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the first element of.
        /// </param>
        /// 
        /// <param name="predicate">
        /// A function to test each element for a condition.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// default(TSource) if source is empty or
        /// if no element passes the test specified by predicate;
        /// otherwise, the first element in source that passes
        /// the test specified by predicate.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source or predicate is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// source is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> FirstOrDefaultAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var filtered = source.Where(predicate);
            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("FirstOrDefaultAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(filtered.Expression)
                                .Take(1);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.FirstOrDefault();
        }

        /// <summary>
        /// Asynchronously returns the only element of a sequence, and throws an exception if there
        /// is not exactly one element in the sequence.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the single element of.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The single element of the input sequence.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// The source sequence is empty, has more than one element or
        /// is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> SingleAsync<TSource>(this IQueryable<TSource> source, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("SingleAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(source.Expression)
                                .Take(2);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.Single();
        }

        /// <summary>
        /// Asynchronously returns the only element of a sequence, and throws an exception if there
        /// is not exactly one element in the sequence.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the single element of.
        /// </param>
        /// 
        /// <param name="predicate">
        /// A function to test each element for a condition.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The single element of the input sequence that satisfies the condition in predicate.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source or predicate is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// No element satisfies the condition in predicate, more than
        /// one element satisfies the condition, the source sequence is empty or
        /// source is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> SingleAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var filtered = source.Where(predicate);
            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("SingleAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(filtered.Expression)
                                .Take(2);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.Single();
        }

        /// <summary>
        /// Asynchronously returns the only element of a sequence, or a default value if the
        /// sequence is empty; this method throws an exception if there is more than one
        /// element in the sequence.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the first element of.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The single element of the input sequence, or default(TSource)
        /// if the sequence contains no elements.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// source has more than one element or
        /// is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> SingleOrDefaultAsync<TSource>(this IQueryable<TSource> source, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("SingleOrDefaultAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(source.Expression)
                                .Take(2);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.SingleOrDefault();
        }

        /// <summary>
        /// Asynchronously returns the only element of a sequence that satisfies a specified
        /// condition or a default value if no such element exists; this method throws an
        /// exception if more than one element satisfies the condition.
        /// </summary>
        /// 
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// 
        /// <param name="source">
        /// The <see cref="IRavenQueryable{T}"/> to return the first element of.
        /// </param>
        /// 
        /// <param name="predicate">
        /// A function to test each element for a condition.
        /// </param>
        /// 
        /// <param name="token">The cancellation token.</param>
        /// 
        /// <returns>
        /// The single element of the input sequence that satisfies the condition in predicate,
        /// or default(TSource) if no such element is found.
        /// </returns>
        /// 
        /// <exception cref="ArgumentNullException">
        /// source or predicate is null.
        /// </exception>
        /// 
        /// <exception cref="InvalidOperationException">
        /// More than one element satisfies the condition in predicate
        /// or source is not of type <see cref="IRavenQueryable{T}"/>.
        /// </exception>
        public static async Task<TSource> SingleOrDefaultAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate, CancellationToken token = default(CancellationToken))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var filtered = source.Where(predicate);
            var provider = source.Provider as IRavenQueryProvider;

            if (provider == null)
                throw new InvalidOperationException("SingleOrDefaultAsync only be used with IRavenQueryable");

            var query = provider.ToAsyncDocumentQuery<TSource>(filtered.Expression)
                                .Take(2);

            provider.MoveAfterQueryExecuted(query);

            var result = await query.ToListAsync(token).ConfigureAwait(false);

            return result.SingleOrDefault();
        }

        /// <summary>
        /// Perform a search for documents which fields that match the searchTerms.
        /// If there is more than a single term, each of them will be checked independently.
        /// </summary>
        public static IRavenQueryable<T> Search<T>(this IQueryable<T> self, Expression<Func<T, object>> fieldSelector, string searchTerms,
                                                   decimal boost = 1,
                                                   SearchOptions options = SearchOptions.Guess)
        {
            var currentMethod = typeof(LinqExtensions).GetMethod(nameof(Search));

            var expression = ConvertExpressionIfNecessary(self);

            var queryable = self.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression,
                                                                      fieldSelector,
                                                                      Expression.Constant(searchTerms),
                                                                      Expression.Constant(boost),
                                                                      Expression.Constant(options)));
            return (IRavenQueryable<T>)queryable;
        }

        /// <summary>
        /// Perform an initial sort by lucene score.
        /// </summary>
        public static IOrderedQueryable<T> OrderByScore<T>(this IQueryable<T> self)
        {
            var currentMethod = typeof(LinqExtensions).GetMethod(nameof(OrderByScore));

            var expression = ConvertExpressionIfNecessary(self);

            var queryable = self.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression));
            return (IOrderedQueryable<T>)queryable;
        }

        /// <summary>
        /// Perform an initial sort by lucene score descending.
        /// </summary>
        public static IOrderedQueryable<T> OrderByScoreDescending<T>(this IQueryable<T> self)
        {
            var currentMethod = typeof(LinqExtensions).GetMethod(nameof(OrderByScoreDescending));

            var expression = ConvertExpressionIfNecessary(self);

            var queryable = self.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression));
            return (IOrderedQueryable<T>)queryable;
        }

        /// <summary>
        /// Returns the query results as a stream
        /// </summary>
        public static void ToStream<T>(this IQueryable<T> self, Stream stream)
        {
            var queryProvider = (IRavenQueryProvider)self.Provider;
            var docQuery = queryProvider.ToDocumentQuery<T>(self.Expression);
            ToStream(docQuery, stream);
        }
        /// <summary> 
        /// Returns the query results as a stream
        /// </summary>
        public static void ToStream<T>(this IDocumentQuery<T> self, Stream stream)
        {
            var documentQuery = (DocumentQuery<T>)self;
            var session = (DocumentSession)documentQuery.Session;
            session.Advanced.StreamInto(self, stream);
        }

        /// <summary>
        /// Returns the query results as a stream
        /// </summary>
        public static async Task ToStreamAsync<T>(this IQueryable<T> self, Stream stream, CancellationToken token = default(CancellationToken))
        {
            var queryProvider = (IRavenQueryProvider)self.Provider;
            var docQuery = queryProvider.ToAsyncDocumentQuery<T>(self.Expression);
            await ToStreamAsync(docQuery, stream, token).ConfigureAwait(false);
        }
        /// <summary>
        /// Returns the query results as a stream
        /// </summary>
        public static async Task ToStreamAsync<T>(this IAsyncDocumentQuery<T> self, Stream stream, CancellationToken token = default(CancellationToken))
        {
            var documentQuery = (AbstractDocumentQuery<T, AsyncDocumentQuery<T>>)self;
            var session = documentQuery.AsyncSession;
            await session.Advanced.StreamIntoAsync(self, stream, token).ConfigureAwait(false);
        }

        public static IRavenQueryable<T> Where<T>(this IQueryable<T> source, Expression<Func<T, int, bool>> predicate, bool exact)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetWhereMethod(3);
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, predicate, Expression.Constant(exact)));
            return (IRavenQueryable<T>)queryable;
        }

        public static IRavenQueryable<T> Where<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate, bool exact)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetWhereMethod(2);
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, predicate, Expression.Constant(exact)));
            return (IRavenQueryable<T>)queryable;
        }

        public static IRavenQueryable<T> Spatial<T>(this IQueryable<T> source, Expression<Func<T, object>> path, Func<SpatialCriteriaFactory, SpatialCriteria> clause)
        {
            return source.Spatial(path.ToPropertyPath(), clause);
        }

        public static IRavenQueryable<T> Spatial<T>(this IQueryable<T> source, string fieldName, Func<SpatialCriteriaFactory, SpatialCriteria> clause)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetSpatialMethod();
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(fieldName), Expression.Constant(clause)));
            return (IRavenQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> OrderByDistance<T>(this IQueryable<T> source, Expression<Func<T, object>> path, double latitude, double longitude)
        {
            return source.OrderByDistance(path.ToPropertyPath(), latitude, longitude);
        }

        public static IOrderedQueryable<T> OrderByDistance<T>(this IQueryable<T> source, string fieldName, double latitude, double longitude)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByDistanceMethod(nameof(OrderByDistance), 4);
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(fieldName), Expression.Constant(latitude), Expression.Constant(longitude)));
            return (IOrderedQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> OrderByDistance<T>(this IQueryable<T> source, Expression<Func<T, object>> path, string shapeWkt)
        {
            return source.OrderByDistance(path.ToPropertyPath(), shapeWkt);
        }

        public static IOrderedQueryable<T> OrderByDistance<T>(this IQueryable<T> source, string fieldName, string shapeWkt)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByDistanceMethod(nameof(OrderByDistance), 3);
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(fieldName), Expression.Constant(shapeWkt)));
            return (IRavenQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> OrderByDistanceDescending<T>(this IQueryable<T> source, Expression<Func<T, object>> path, double latitude, double longitude)
        {
            return source.OrderByDistanceDescending(path.ToPropertyPath(), latitude, longitude);
        }

        public static IOrderedQueryable<T> OrderByDistanceDescending<T>(this IQueryable<T> source, string fieldName, double latitude, double longitude)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByDistanceMethod(nameof(OrderByDistanceDescending), 4);
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(fieldName), Expression.Constant(latitude), Expression.Constant(longitude)));
            return (IRavenQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> OrderByDistanceDescending<T>(this IQueryable<T> source, Expression<Func<T, object>> path, string shapeWkt)
        {
            return source.OrderByDistanceDescending(path.ToPropertyPath(), shapeWkt);
        }

        public static IOrderedQueryable<T> OrderByDistanceDescending<T>(this IQueryable<T> source, string fieldName, string shapeWkt)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByDistanceMethod(nameof(OrderByDistanceDescending), 3);
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(fieldName), Expression.Constant(shapeWkt)));
            return (IOrderedQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, Expression<Func<T, object>> path, OrderingType ordering = OrderingType.String)
        {
            return source.OrderBy(path.ToPropertyPath(), ordering);
        }

        public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, string path, OrderingType ordering = OrderingType.String)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByMethod(nameof(OrderBy));
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(path), Expression.Constant(ordering)));
            return (IOrderedQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> OrderByDescending<T>(this IQueryable<T> source, Expression<Func<T, object>> path, OrderingType ordering = OrderingType.String)
        {
            return source.OrderByDescending(path.ToPropertyPath(), ordering);
        }

        public static IOrderedQueryable<T> OrderByDescending<T>(this IQueryable<T> source, string path, OrderingType ordering = OrderingType.String)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByMethod(nameof(OrderByDescending));
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(path), Expression.Constant(ordering)));
            return (IOrderedQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, Expression<Func<T, object>> path, OrderingType ordering = OrderingType.String)
        {
            return source.ThenBy(path.ToPropertyPath(), ordering);
        }

        public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, string path, OrderingType ordering = OrderingType.String)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByMethod(nameof(ThenBy));
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(path), Expression.Constant(ordering)));
            return (IOrderedQueryable<T>)queryable;
        }

        public static IOrderedQueryable<T> ThenByDescending<T>(this IOrderedQueryable<T> source, Expression<Func<T, object>> path, OrderingType ordering = OrderingType.String)
        {
            return source.ThenByDescending(path.ToPropertyPath(), ordering);
        }

        public static IOrderedQueryable<T> ThenByDescending<T>(this IOrderedQueryable<T> source, string path, OrderingType ordering = OrderingType.String)
        {
#if NETSTANDARD2_0
            var currentMethod = (MethodInfo)MethodBase.GetCurrentMethod();
#else
            var currentMethod = GetOrderByMethod(nameof(ThenByDescending));
#endif

            var expression = ConvertExpressionIfNecessary(source);

            var queryable = source.Provider.CreateQuery(Expression.Call(null, currentMethod.MakeGenericMethod(typeof(T)), expression, Expression.Constant(path), Expression.Constant(ordering)));
            return (IOrderedQueryable<T>)queryable;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Expression ConvertExpressionIfNecessary<T>(IQueryable<T> source)
        {
            var expression = source.Expression;
            if (expression.Type != typeof(IRavenQueryable<T>))
                expression = Expression.Convert(expression, typeof(IRavenQueryable<T>));

            return expression;
        }

#if !NETSTANDARD2_0
        private static MethodInfo GetOrderByMethod(string methodName)
        {
            var orderByMethod = GetOrderByMethodInfo(methodName);
            if (orderByMethod != null)
                return orderByMethod;

            lock (Locker)
            {
                orderByMethod = GetOrderByMethodInfo(methodName);
                if (orderByMethod != null)
                    return orderByMethod;

                foreach (var method in typeof(LinqExtensions).GetMethods())
                {
                    if (method.Name != methodName)
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length != 3)
                        continue;

                    if (parameters[1].ParameterType != typeof(string))
                        continue;

                    SetOrderByMethodInfo(methodName, method);
                    break;
                }

                return GetOrderByMethodInfo(methodName);
            }
        }

        private static void SetOrderByMethodInfo(string methodName, MethodInfo method)
        {
            switch (methodName)
            {
                case nameof(OrderBy):
                    _orderByMethod = method;
                    break;
                case nameof(OrderByDescending):
                    _orderByDescendingMethod = method;
                    break;
                case nameof(ThenBy):
                    _thenByMethod = method;
                    break;
                case nameof(ThenByDescending):
                    _thenByDescendingMethod = method;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static MethodInfo GetOrderByMethodInfo(string methodName)
        {
            switch (methodName)
            {
                case nameof(OrderBy):
                    return _orderByMethod;
                case nameof(OrderByDescending):
                    return _orderByDescendingMethod;
                case nameof(ThenBy):
                    return _thenByMethod;
                case nameof(ThenByDescending):
                    return _thenByDescendingMethod;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static MethodInfo GetOrderByDistanceMethod(string methodName, int numberOfParameters)
        {
            var orderByDistanceMethod = GetOrderByDistanceMethodInfo(methodName);
            if (orderByDistanceMethod != null)
                return orderByDistanceMethod;

            lock (Locker)
            {
                orderByDistanceMethod = GetOrderByDistanceMethodInfo(methodName);
                if (orderByDistanceMethod != null)
                    return orderByDistanceMethod;

                foreach (var method in typeof(LinqExtensions).GetMethods())
                {
                    if (method.Name != methodName)
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length != numberOfParameters)
                        continue;

                    if (parameters[1].ParameterType != typeof(string))
                        continue;

                    SetOrderByDistanceMethodInfo(methodName, method);
                    break;
                }

                return GetOrderByDistanceMethodInfo(methodName);
            }
        }

        private static void SetOrderByDistanceMethodInfo(string methodName, MethodInfo method)
        {
            switch (methodName)
            {
                case nameof(OrderByDistance):
                    _orderByDistanceMethod = method;
                    break;
                case nameof(OrderByDistanceDescending):
                    _orderByDistanceDescendingMethod = method;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static MethodInfo GetOrderByDistanceMethodInfo(string methodName)
        {
            switch (methodName)
            {
                case nameof(OrderByDistance):
                    return _orderByDistanceMethod;
                case nameof(OrderByDistanceDescending):
                    return _orderByDistanceDescendingMethod;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static MethodInfo GetSpatialMethod()
        {
            var spatialMethod = _spatialMethod;
            if (spatialMethod != null)
                return spatialMethod;

            lock (Locker)
            {
                spatialMethod = _spatialMethod;
                if (spatialMethod != null)
                    return spatialMethod;

                foreach (var method in typeof(LinqExtensions).GetMethods())
                {
                    if (method.Name != nameof(Spatial))
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length != 3)
                        continue;

                    if (parameters[1].ParameterType != typeof(string))
                        continue;

                    _spatialMethod = method;
                    break;
                }

                return _spatialMethod;
            }
        }

        private static MethodInfo GetWhereMethod(int numberOfFuncArguments)
        {
            var whereMethod = GetWhereMethodInfo(numberOfFuncArguments);
            if (whereMethod != null)
                return whereMethod;

            lock (Locker)
            {
                whereMethod = GetWhereMethodInfo(numberOfFuncArguments);
                if (whereMethod != null)
                    return whereMethod;

                foreach (var method in typeof(LinqExtensions).GetMethods())
                {
                    if (method.Name != nameof(Where))
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length != 3)
                        continue;

                    var predicate = parameters[1];
                    var func = predicate.ParameterType.GenericTypeArguments[0];
                    if (func.GenericTypeArguments.Length != numberOfFuncArguments)
                        continue;

                    SetWhereMethodInfo(numberOfFuncArguments, method);
                    break;
                }

                return GetWhereMethodInfo(numberOfFuncArguments);
            }
        }

        private static MethodInfo GetWhereMethodInfo(int numberOfFuncArguments)
        {
            switch (numberOfFuncArguments)
            {
                case 2:
                    return _whereMethod2;
                case 3:
                    return _whereMethod3;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void SetWhereMethodInfo(int numberOfFuncArguments, MethodInfo methodInfo)
        {
            switch (numberOfFuncArguments)
            {
                case 2:
                    _whereMethod2 = methodInfo;
                    break;
                case 3:
                    _whereMethod3 = methodInfo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static MethodInfo GetIncludeMethod()
        {
            var includeMethod = _includeMethod;
            if (includeMethod != null)
                return includeMethod;

            lock (Locker)
            {
                includeMethod = _includeMethod;
                if (includeMethod != null)
                    return includeMethod;

                foreach (var method in typeof(LinqExtensions).GetMethods())
                {
                    if (method.Name != nameof(Include))
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length != 2)
                        continue;

                    if (parameters[1].ParameterType != typeof(string))
                        continue;

                    _includeMethod = method;
                    break;
                }

                return _includeMethod;
            }
        }
#endif
    }
}
