﻿using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Queries.MoreLikeThis;
using Raven.Client.Documents.Transformers;
using Raven.Client.Extensions;
using Sparrow.Json;

namespace Raven.Client.Documents.Session.Operations
{
    internal class MoreLikeThisOperation
    {
        private readonly InMemoryDocumentSessionOperations _session;
        private readonly MoreLikeThisQuery _query;

        private MoreLikeThisQueryResult _result;

        public MoreLikeThisOperation(InMemoryDocumentSessionOperations session, MoreLikeThisQuery query)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _query = query ?? throw new ArgumentNullException(nameof(query));

            if (_query.IndexName == null)
                throw new ArgumentNullException(nameof(query.IndexName));
        }

        public MoreLikeThisCommand CreateRequest()
        {
            _session.IncrementRequestCount();

            return new MoreLikeThisCommand(_query);
        }

        public void SetResult(MoreLikeThisQueryResult result)
        {
            _result = result;
        }

        public List<T> Complete<T>()
        {
            foreach (BlittableJsonReaderObject include in _result.Includes)
            {
                if (include == null)
                    continue;

                var newDocumentInfo = DocumentInfo.GetNewDocumentInfo(include);
                _session.IncludedDocumentsByKey[newDocumentInfo.Id] = newDocumentInfo;
            }

            var usedTransformer = string.IsNullOrEmpty(_query.Transformer) == false;
            List<T> list;
            if (usedTransformer)
            {
                list = TransformerHelper.ParseResultsForQueryOperation<T>(_session, _result).ToList();
            }
            else
            {
                list = new List<T>();
                foreach (BlittableJsonReaderObject document in _result.Results)
                {
                    var metadata = document.GetMetadata();

                    string id;
                    metadata.TryGetId(out id);

                    list.Add(QueryOperation.Deserialize<T>(id, document, metadata, projectionFields: null, disableEntitiesTracking: false, session: _session));
                }
            }

            _session.RegisterMissingIncludes(_result.Results, _query.Includes);

            return list;
        }
    }
}