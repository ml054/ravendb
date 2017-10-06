﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Raven.Client;
using Raven.Server.Documents.Includes;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Raven.Server.Utils;
using Sparrow;
using Sparrow.Json;

namespace Raven.Server.Documents.Queries.Dynamic
{
    public class CollectionQueryRunner : AbstractQueryRunner
    {
        public const string CollectionIndexPrefix = "collection/";

        public CollectionQueryRunner(DocumentDatabase database) : base(database)
        {
        }

        public override Task<DocumentQueryResult> ExecuteQuery(IndexQueryServerSide query, DocumentsOperationContext documentsContext, long? existingResultEtag, OperationCancelToken token)
        {
            var result = new DocumentQueryResult();

            documentsContext.OpenReadTransaction();

            FillCountOfResultsAndIndexEtag(result, query.Metadata.CollectionName, documentsContext);

            if (existingResultEtag.HasValue)
            {
                if (result.ResultEtag == existingResultEtag)
                    return Task.FromResult(DocumentQueryResult.NotModifiedResult);
            }

            ExecuteCollectionQuery(result, query, query.Metadata.CollectionName, documentsContext, token.Token);

            return Task.FromResult(result);
        }

        public override Task ExecuteStreamQuery(IndexQueryServerSide query, DocumentsOperationContext documentsContext, HttpResponse response, BlittableJsonTextWriter writer,
            OperationCancelToken token)
        {
            using (var result = new StreamDocumentQueryResult(response, writer, documentsContext))
            {
                documentsContext.OpenReadTransaction();

                FillCountOfResultsAndIndexEtag(result, query.Metadata.CollectionName, documentsContext);

                ExecuteCollectionQuery(result, query, query.Metadata.CollectionName, documentsContext, token.Token);
            }

            return Task.CompletedTask;
        }

        public override Task<IndexEntriesQueryResult> ExecuteIndexEntriesQuery(IndexQueryServerSide query, DocumentsOperationContext context, long? existingResultEtag, OperationCancelToken token)
        {
            throw new System.NotImplementedException();
        }

        private void ExecuteCollectionQuery(QueryResultServerSide resultToFill, IndexQueryServerSide query, string collection, DocumentsOperationContext context, CancellationToken cancellationToken)
        {
            var isAllDocsCollection = collection == Constants.Documents.Collections.AllDocumentsCollection;

            // we optimize for empty queries without sorting options, appending CollectionIndexPrefix to be able to distinguish index for collection vs. physical index
            resultToFill.IndexName = isAllDocsCollection ? "AllDocs" : CollectionIndexPrefix + collection;
            resultToFill.IsStale = false;
            resultToFill.LastQueryTime = DateTime.MinValue;
            resultToFill.IndexTimestamp = DateTime.MinValue;
            resultToFill.IncludedPaths = query.Metadata.Includes;

            var includeDocumentsCommand = new IncludeDocumentsCommand(Database.DocumentsStorage, context, query.Metadata.Includes);
            var fieldsToFetch = new FieldsToFetch(query, null);
            var totalResults = new Reference<int>();
            var documents = new CollectionQueryEnumerable(Database, Database.DocumentsStorage, fieldsToFetch, collection, query, context, includeDocumentsCommand, totalResults);

            try
            {
                foreach (var document in documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    resultToFill.AddResult(document);

                    includeDocumentsCommand.Gather(document);
                }
            }
            catch (Exception e)
            {
                if (resultToFill.SupportsExceptionHandling == false)
                    throw;

                resultToFill.HandleException(e);
            }

            includeDocumentsCommand.Fill(resultToFill.Includes);
            resultToFill.TotalResults = totalResults.Value;
        }

        private unsafe void FillCountOfResultsAndIndexEtag(QueryResultServerSide resultToFill, string collection, DocumentsOperationContext context)
        {
            var buffer = stackalloc long[3];

            if (collection == Constants.Documents.Collections.AllDocumentsCollection)
            {
                var numberOfDocuments = Database.DocumentsStorage.GetNumberOfDocuments(context);
                buffer[0] = DocumentsStorage.ReadLastDocumentEtag(context.Transaction.InnerTransaction);
                buffer[1] = DocumentsStorage.ReadLastTombstoneEtag(context.Transaction.InnerTransaction);
                buffer[2] = numberOfDocuments;
                resultToFill.TotalResults = (int)numberOfDocuments;
            }
            else
            {
                var collectionStats = Database.DocumentsStorage.GetCollection(collection, context);

                buffer[0] = Database.DocumentsStorage.GetLastDocumentEtag(context, collection);
                buffer[1] = Database.DocumentsStorage.GetLastTombstoneEtag(context, collection);
                buffer[2] = collectionStats.Count;
            }

            resultToFill.ResultEtag = (long)Hashing.XXHash64.Calculate((byte*)buffer, sizeof(long) * 3);
        }
    }
}
