//-----------------------------------------------------------------------
// <copyright file="IRavenQueryProvider.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.Spatial;
using Raven.Client.Documents.Session;

namespace Raven.Client.Documents.Linq
{
    /// <summary>
    /// Extension for the built-in <see cref="IQueryProvider"/> allowing for Raven specific operations
    /// </summary>
    public interface IRavenQueryProvider : IQueryProvider
    {
        /// <summary>
        /// Callback to get the results of the query
        /// </summary>
        void AfterQueryExecuted(Action<QueryResult> afterQueryExecuted);

        /// <summary>
        /// Customizes the query using the specified action
        /// </summary>
        void Customize(Action<IDocumentQueryCustomization> action);

        /// <summary>
        /// The name of the transformer to use with this query
        /// </summary>
        /// <param name="transformerName"></param>
        void TransformWith(string transformerName);

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        /// <value>The name of the index.</value>
        string IndexName { get; }

        /// <summary>
        /// Get the query generator
        /// </summary>
        IDocumentQueryGenerator QueryGenerator { get; }

        /// <summary>
        /// The action to execute on the customize query
        /// </summary>
        Action<IDocumentQueryCustomization> CustomizeQuery { get; }

        /// <summary>
        /// Change the result type for the query provider
        /// </summary>
        IRavenQueryProvider For<TS>();

        /// <summary>
        /// Convert the Linq query to a Lucene query
        /// </summary>
        IAsyncDocumentQuery<T> ToAsyncDocumentQuery<T>(Expression expression);

        /// <summary>
        /// Convert the linq query to a Lucene query
        /// </summary>
        IDocumentQuery<TResult> ToDocumentQuery<TResult>(Expression expression);

        /// <summary>
        /// Convert the Linq query to a lazy Lucene query and provide a function to execute when it is being evaluated
        /// </summary>
        Lazy<IEnumerable<T>> Lazily<T>(Expression expression, Action<IEnumerable<T>> onEval);

        Lazy<Task<IEnumerable<T>>> LazilyAsync<T>(Expression expression, Action<IEnumerable<T>> onEval);

        /// <summary>
        /// Convert the Linq query to a lazy-count Lucene query and provide a function to execute when it is being evaluated
        /// </summary>
        Lazy<int> CountLazily<T>(Expression expression);

        /// <summary>
        /// Convert the Linq query to a lazy-count Lucene query and provide a function to execute when it is being evaluated
        /// </summary>
        Lazy<Task<int>> CountLazilyAsync<T>(Expression expression, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Move the registered after query actions
        /// </summary>
        void MoveAfterQueryExecuted<T>(IAsyncDocumentQuery<T> documentQuery);

        /// <summary>
        /// Set the fields to fetch
        /// </summary>
        HashSet<FieldToFetch> FieldsToFetch { get; }

        /// <summary>
        /// The result transformer to use
        /// </summary>
        string ResultTransformer { get; }

        /// <summary>
        /// Gets the query inputs being supplied to
        /// </summary>
        Parameters TransformerParameters { get; }

        /// <summary>
        /// Adds input to transformer via a key/value pair
        /// </summary>
        /// <param name="input"></param>
        /// <param name="value"></param>
        void AddTransformerParameter(string input, object value);

        void AddTransformerParameter(string input, DateTime value);

        Type OriginalQueryType { get; }
    }
}
