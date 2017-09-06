﻿using System;
using System.Net.Http;
using System.Text;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Queries;
using Raven.Client.Extensions;
using Raven.Client.Http;
using Raven.Client.Json;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Commands
{
    public class QueryCommand : RavenCommand<QueryResult>
    {
        private readonly DocumentConventions _conventions;
        private readonly IndexQuery _indexQuery;
        private readonly bool _metadataOnly;
        private readonly bool _indexEntriesOnly;

        public QueryCommand(DocumentConventions conventions, IndexQuery indexQuery, bool metadataOnly = false, bool indexEntriesOnly = false)
        {
            _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
            _indexQuery = indexQuery ?? throw new ArgumentNullException(nameof(indexQuery));
            _metadataOnly = metadataOnly;
            _indexEntriesOnly = indexEntriesOnly;

            if (indexQuery.WaitForNonStaleResultsTimeout.HasValue && indexQuery.WaitForNonStaleResultsTimeout != TimeSpan.MaxValue)
                Timeout = indexQuery.WaitForNonStaleResultsTimeout.Value.Add(TimeSpan.FromSeconds(10)); // giving the server an opportunity to finish the response
        }

        public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
        {
            CanCache = _indexQuery.DisableCaching == false;

            // we won't allow aggresive caching of queries with WaitForNonStaleResults
            CanCacheAggressively = CanCache && _indexQuery.WaitForNonStaleResults == false;

            var path = new StringBuilder(node.Url)
                .Append("/databases/")
                .Append(node.Database)
                .Append("/queries?query-hash=")
                // we need to add a query hash because we are using POST queries
                // so we need to unique parameter per query so the query cache will
                // work properly
                .Append(_indexQuery.GetQueryHash(ctx));

            if (_metadataOnly)
                path.Append("&metadata-only=true");

            if (_indexEntriesOnly)
            {
                path.Append("&debug=entries");
            }

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content = new BlittableJsonContent(stream =>
                    {
                        using (var writer = new BlittableJsonTextWriter(ctx, stream))
                        {
                            writer.WriteIndexQuery(_conventions, ctx, _indexQuery);
                        }
                    }
                )
            };

            url = path.ToString();
            return request;
        }

        public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
        {
            if (response == null)
            {
                Result = null;
                return;
            }

            Result = JsonDeserializationClient.QueryResult(response);

            if (fromCache)
                Result.DurationInMs = -1;
        }

        public override bool IsReadRequest => true;
    }
}
