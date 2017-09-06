﻿using System;
using Lucene.Net.Store;
using Raven.Client;
using Raven.Server.Documents.Includes;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Documents.Queries.Results
{
    public class MapQueryResultRetriever : QueryResultRetrieverBase
    {

        private readonly DocumentsOperationContext _context;

        public MapQueryResultRetriever(DocumentDatabase database,IndexQueryServerSide query, DocumentsStorage documentsStorage, DocumentsOperationContext context, FieldsToFetch fieldsToFetch, IncludeDocumentsCommand includeDocumentsCommand)
            : base(database,query, fieldsToFetch, documentsStorage, context, false, includeDocumentsCommand)
        {
            _context = context;
        }

        public override Document Get(Lucene.Net.Documents.Document input, float score, IState state)
        {
            if (TryGetKey(input, state, out string id) == false)
                throw new InvalidOperationException($"Could not extract '{Constants.Documents.Indexing.Fields.DocumentIdFieldName}' from index.");

            if (FieldsToFetch.IsProjection)
                return GetProjection(input, score, id, state);

            var doc = DirectGet(null, id, state);

            if (doc != null)
                doc.IndexScore = score;

            return doc;
        }

        public override bool TryGetKey(Lucene.Net.Documents.Document input, IState state, out string key)
        {
            key = input.Get(Constants.Documents.Indexing.Fields.DocumentIdFieldName, state);
            return key != null;
        }

        protected override Document DirectGet(Lucene.Net.Documents.Document input, string id, IState state)
        {
            return DocumentsStorage.Get(_context, id);
        }

        protected override Document LoadDocument(string id)
        {
            return DocumentsStorage.Get(_context, id);
        }
    }
}
