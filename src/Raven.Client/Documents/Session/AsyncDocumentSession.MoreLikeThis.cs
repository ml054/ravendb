//-----------------------------------------------------------------------
// <copyright file="AsyncDocumentSession.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Queries.MoreLikeThis;
using Raven.Client.Documents.Session.Operations;
using Raven.Client.Documents.Transformers;

namespace Raven.Client.Documents.Session
{
    /// <summary>
    /// Implementation for async document session 
    /// </summary>
    public partial class AsyncDocumentSession
    {
        public Task<List<T>> MoreLikeThisAsync<T, TIndexCreator>(string documentId) where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (documentId == null)
                throw new ArgumentNullException(nameof(documentId));

            var index = new TIndexCreator();
            return MoreLikeThisAsync<T>(new MoreLikeThisQuery { IndexName = index.IndexName, DocumentId = documentId });
        }

        public Task<List<T>> MoreLikeThisAsync<T, TIndexCreator>(MoreLikeThisQuery query) where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var index = new TIndexCreator();
            query.IndexName = index.IndexName;
            return MoreLikeThisAsync<T>(query);
        }

        public Task<List<T>> MoreLikeThisAsync<TTransformer, T, TIndexCreator>(string documentId, Dictionary<string, object> transformerParameters = null) where TTransformer : AbstractTransformerCreationTask, new() where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (documentId == null)
                throw new ArgumentNullException(nameof(documentId));

            var index = new TIndexCreator();
            var transformer = new TTransformer();

            return MoreLikeThisAsync<T>(new MoreLikeThisQuery
            {
                IndexName = index.IndexName,
                Transformer = transformer.TransformerName,
                TransformerParameters = transformerParameters
            });
        }

        public Task<List<T>> MoreLikeThisAsync<TTransformer, T, TIndexCreator>(MoreLikeThisQuery query) where TTransformer : AbstractTransformerCreationTask, new() where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var index = new TIndexCreator();
            var transformer = new TTransformer();

            query.IndexName = index.IndexName;
            query.Transformer = transformer.TransformerName;

            return MoreLikeThisAsync<T>(query);
        }

        public Task<List<T>> MoreLikeThisAsync<T>(string index, string documentId, string transformer = null, Dictionary<string, object> transformerParameters = null)
        {
            return MoreLikeThisAsync<T>(new MoreLikeThisQuery
            {
                IndexName = index,
                DocumentId = documentId,
                Transformer = transformer,
                TransformerParameters = transformerParameters
            });
        }

        public async Task<List<T>> MoreLikeThisAsync<T>(MoreLikeThisQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var operation = new MoreLikeThisOperation(this, query);

            var command = operation.CreateRequest();
            await RequestExecutor.ExecuteAsync(command, Context).ConfigureAwait(false);

            var result = command.Result;
            operation.SetResult(result);

            return operation.Complete<T>();
        }
    }
}
