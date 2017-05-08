﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Exceptions;
using Raven.Client.Documents.Exceptions.Indexes;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.Facets;
using Raven.Client.Util.RateLimiting;
using Raven.Server.Documents.Indexes;
using Raven.Server.Documents.Queries.Dynamic;
using Raven.Server.Documents.Queries.MoreLikeThis;
using Raven.Server.Json;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

using PatchRequest = Raven.Server.Documents.Patch.PatchRequest;

namespace Raven.Server.Documents.Queries
{
    public class QueryRunner
    {
        private readonly DocumentDatabase _database;

        private readonly DocumentsOperationContext _documentsContext;

        public QueryRunner(DocumentDatabase database, DocumentsOperationContext documentsContext)
        {
            _database = database;
            _documentsContext = documentsContext;
        }

        public async Task<DocumentQueryResult> ExecuteQuery(string indexName, IndexQueryServerSide query, StringValues includes, long? existingResultEtag, OperationCancelToken token)
        {
            DocumentQueryResult result;
            var sw = Stopwatch.StartNew();
            if (DynamicQueryRunner.IsDynamicIndex(indexName))
            {
                var runner = new DynamicQueryRunner(_database.IndexStore, _database.TransformerStore, _database.DocumentsStorage, _documentsContext, token);

                result = await runner.Execute(indexName, query, existingResultEtag);
                result.DurationMilliseconds = (long)sw.Elapsed.TotalMilliseconds;
                return result;
            }

            var index = GetIndex(indexName);
            if (existingResultEtag.HasValue)
            {
                var etag = index.GetIndexEtag();
                if (etag == existingResultEtag)
                    return DocumentQueryResult.NotModifiedResult;
            }

            result = await index.Query(query, _documentsContext, token);
            result.DurationMilliseconds = (long)sw.Elapsed.TotalMilliseconds;
            return result;
        }

        public async Task ExecuteStreamQuery(string indexName, IndexQueryServerSide query, HttpResponse response, BlittableJsonTextWriter writer, OperationCancelToken token)
        {
            if (DynamicQueryRunner.IsDynamicIndex(indexName))
            {
                var runner = new DynamicQueryRunner(_database.IndexStore, _database.TransformerStore, _database.DocumentsStorage, _documentsContext, token);

                await runner.ExecuteStream(response, writer, indexName, query).ConfigureAwait(false);
            }

            var index = GetIndex(indexName);

            await index.StreamQuery(response, writer, query, _documentsContext, token);
        }

        public Task<FacetedQueryResult> ExecuteFacetedQuery(string indexName, FacetQuery query, long? facetsEtag, long? existingResultEtag, OperationCancelToken token)
        {
            if (query.FacetSetupDoc != null)
            {
                FacetSetup facetSetup;
                using (_documentsContext.OpenReadTransaction())
                {
                    var facetSetupAsJson = _database.DocumentsStorage.Get(_documentsContext, query.FacetSetupDoc);
                    if (facetSetupAsJson == null)
                        throw new DocumentDoesNotExistException(query.FacetSetupDoc);

                    try
                    {
                        facetSetup = JsonDeserializationServer.FacetSetup(facetSetupAsJson.Data);
                    }
                    catch (Exception e)
                    {
                        throw new DocumentParseException(query.FacetSetupDoc, typeof(FacetSetup), e);
                    }

                    facetsEtag = facetSetupAsJson.Etag;
                }

                query.Facets = facetSetup.Facets;
            }

            return ExecuteFacetedQuery(indexName, query, facetsEtag.Value, existingResultEtag, token);
        }

        private async Task<FacetedQueryResult> ExecuteFacetedQuery(string indexName, FacetQuery query, long facetsEtag, long? existingResultEtag, OperationCancelToken token)
        {
            var index = GetIndex(indexName);
            if (existingResultEtag.HasValue)
            {
                var etag = index.GetIndexEtag() ^ facetsEtag;
                if (etag == existingResultEtag)
                    return FacetedQueryResult.NotModifiedResult;
            }

            return await index.FacetedQuery(query, facetsEtag, _documentsContext, token);
        }

        public TermsQueryResultServerSide ExecuteGetTermsQuery(string indexName, string field, string fromValue, long? existingResultEtag, int pageSize, DocumentsOperationContext context, OperationCancelToken token)
        {
            var index = GetIndex(indexName);

            var etag = index.GetIndexEtag();
            if (etag == existingResultEtag)
                return TermsQueryResultServerSide.NotModifiedResult;

            return index.GetTerms(field, fromValue, pageSize, context, token);
        }

        public MoreLikeThisQueryResultServerSide ExecuteMoreLikeThisQuery(string indexName, MoreLikeThisQueryServerSide query, DocumentsOperationContext context, long? existingResultEtag, OperationCancelToken token)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrEmpty(query.DocumentId) && query.MapGroupFields.Count == 0)
                throw new InvalidOperationException("The document id or map group fields are mandatory");

            var index = GetIndex(indexName);

            var etag = index.GetIndexEtag();
            if (etag == existingResultEtag)
                return MoreLikeThisQueryResultServerSide.NotModifiedResult;

            context.OpenReadTransaction();

            return index.MoreLikeThisQuery(query, context, token);
        }

        public async Task<IndexEntriesQueryResult> ExecuteIndexEntriesQuery(string indexName, IndexQueryServerSide query, long? existingResultEtag, OperationCancelToken token)
        {
            if (DynamicQueryRunner.IsDynamicIndex(indexName))
            {
                var runner = new DynamicQueryRunner(_database.IndexStore, _database.TransformerStore, _database.DocumentsStorage, _documentsContext, token);
                return await runner.ExecuteIndexEntries(indexName, query, existingResultEtag);
            }

            var index = GetIndex(indexName);

            if (existingResultEtag.HasValue)
            {
                var etag = index.GetIndexEtag();
                if (etag == existingResultEtag)
                    return IndexEntriesQueryResult.NotModifiedResult;
            }

            return index.IndexEntries(query, _documentsContext, token);
        }

        public List<DynamicQueryToIndexMatcher.Explanation> ExplainDynamicIndexSelection(string indexName, IndexQueryServerSide indexQuery)
        {
            if (DynamicQueryRunner.IsDynamicIndex(indexName) == false)
                throw new InvalidOperationException("Explain can only work on dynamic indexes");

            var runner = new DynamicQueryRunner(_database.IndexStore, _database.TransformerStore, _database.DocumentsStorage, _documentsContext, OperationCancelToken.None);

            return runner.ExplainIndexSelection(indexName, indexQuery);
        }

        public Task<IOperationResult> ExecuteDeleteQuery(string indexName, IndexQueryServerSide query, QueryOperationOptions options, DocumentsOperationContext context, Action<DeterminateProgress> onProgress, OperationCancelToken token)
        {
            return ExecuteOperation(indexName, query, options, context, onProgress, (key, ctx, retrieveDetails) =>
            {
                var result = _database.DocumentsStorage.Delete(ctx, key, null);
                if (retrieveDetails && result != null)
                {
                    return new BulkOperationResult.DeleteDetails
                    {
                        Id = key,
                        Etag = result.Value.Etag
                    };
                }

                return null;
            }, token);
        }

        public Task<IOperationResult> ExecutePatchQuery(string indexName, IndexQueryServerSide query, QueryOperationOptions options, PatchRequest patch, DocumentsOperationContext context, Action<DeterminateProgress> onProgress, OperationCancelToken token)
        {
            return ExecuteOperation(indexName, query, options, context, onProgress, (key, ctx, retrieveDetails) =>
            {
                var result = _database.Patcher.Apply(ctx, key, etag: null, patch: patch, patchIfMissing: null, skipPatchIfEtagMismatch: false, debugMode: false);
                if (retrieveDetails && result != null)
                {
                    return new BulkOperationResult.PatchDetails
                    {
                        Id = key,
                        Etag = result.Etag,
                        Status = result.Status
                    };
                }

                return null;
            }, token);
        }

        private async Task<IOperationResult> ExecuteOperation(string indexName, IndexQueryServerSide query, QueryOperationOptions options,
            DocumentsOperationContext context, Action<DeterminateProgress> onProgress, Func<string, DocumentsOperationContext, bool, IBulkOperationDetails> func, OperationCancelToken token)
        {
            var index = GetIndex(indexName);

            if (index.Type.IsMapReduce())
                throw new InvalidOperationException("Cannot execute bulk operation on Map-Reduce indexes.");

            query = ConvertToOperationQuery(query, options);

            const int batchSize = 1024;

            Queue<string> resultIds;
            try
            {
                var results = await index.Query(query, context, token).ConfigureAwait(false);
                if (options.AllowStale == false && results.IsStale)
                    throw new InvalidOperationException("Cannot perform bulk operation. Query is stale.");

                resultIds = new Queue<string>(results.Results.Count);

                foreach (var document in results.Results)
                {
                    resultIds.Enqueue(document.Key.ToString());
                }
            }
            finally // make sure to close tx if DocumentConflictException is thrown
            {
                context.CloseTransaction();
            }
            
            var progress = new DeterminateProgress
            {
                Total = resultIds.Count,
                Processed = 0
            };

            onProgress(progress);

            var result = new BulkOperationResult();

            using (var rateGate = options.MaxOpsPerSecond.HasValue ? new RateGate(options.MaxOpsPerSecond.Value, TimeSpan.FromSeconds(1)) : null)
            {
                while (resultIds.Count > 0)
                {
                    IBulkOperationDetails details = null;

                    var command = new ExecuteRateLimitedOperations<string>(resultIds, (id, ctx) =>
                    {
                        details = func(id, ctx, options.RetrieveDetails);
                    }, rateGate, token, batchSize);

                    await _database.TxMerger.Enqueue(command);

                    if (options.RetrieveDetails && details != null)
                        result.Details.Add(details);

                    progress.Processed += command.Processed;

                    onProgress(progress);

                    if (command.NeedWait)
                        rateGate?.WaitToProceed();
                }
            }

            result.Total = progress.Total;
            return result;
        }

        private static IndexQueryServerSide ConvertToOperationQuery(IndexQueryServerSide query, QueryOperationOptions options)
        {
            return new IndexQueryServerSide
            {
                Query = query.Query,
                Start = query.Start,
                WaitForNonStaleResultsTimeout = options.StaleTimeout,
                PageSize = int.MaxValue,
                SortedFields = query.SortedFields,
                HighlighterPreTags = query.HighlighterPreTags,
                HighlighterPostTags = query.HighlighterPostTags,
                HighlightedFields = query.HighlightedFields,
                HighlighterKeyName = query.HighlighterKeyName,
                TransformerParameters = query.TransformerParameters,
                Transformer = query.Transformer
            };
        }

        private Index GetIndex(string indexName)
        {
            var index = _database.IndexStore.GetIndex(indexName);
            if (index == null)
                IndexDoesNotExistException.ThrowFor(indexName);

            return index;
        }
    }
}