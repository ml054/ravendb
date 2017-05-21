﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Queries;
using Raven.Client.Http;
using Raven.Client.Json;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Commands
{
    public class QueryCommand : RavenCommand<QueryResult>
    {
        private readonly DocumentConventions _conventions;
        private readonly JsonOperationContext _context;
        private readonly string _indexName;
        private readonly IndexQuery _indexQuery;
        private readonly HashSet<string> _includes;
        private readonly bool _metadataOnly;
        private readonly bool _indexEntriesOnly;

        public QueryCommand(DocumentConventions conventions, JsonOperationContext context, string indexName, IndexQuery indexQuery, HashSet<string> includes = null, bool metadataOnly = false, bool indexEntriesOnly = false)
        {
            if (indexQuery == null)
                throw new ArgumentNullException(nameof(indexQuery));

            _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
            _indexQuery = indexQuery;
            _includes = includes;
            _metadataOnly = metadataOnly;
            _indexEntriesOnly = indexEntriesOnly;

            if (_indexQuery.WaitForNonStaleResultsTimeout.HasValue && _indexQuery.WaitForNonStaleResultsTimeout != TimeSpan.MaxValue)
                Timeout = _indexQuery.WaitForNonStaleResultsTimeout.Value.Add(TimeSpan.FromSeconds(10)); // giving the server an opportunity to finish the response
        }

        public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
        {
            var method = _indexQuery.Query == null || _indexQuery.Query.Length <= _conventions.MaxLengthOfQueryUsingGetUrl
                ? HttpMethod.Get
                : HttpMethod.Post;

            var request = new HttpRequestMessage
            {
                Method = method
            };

            if (method == HttpMethod.Post)
            {
                request.Content = new BlittableJsonContent(stream =>
                {
                    using (var writer = new BlittableJsonTextWriter(_context, stream))
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("Query");
                        writer.WriteString(_indexQuery.Query);
                        writer.WriteEndObject();
                    }
                });
            }

            var indexQueryUrl = _indexQuery.GetIndexQueryUrl(_indexName, "queries", includeQuery: method == HttpMethod.Get);

            EnsureIsNotNullOrEmpty(indexQueryUrl, "index");

            var pathBuilder = new StringBuilder(indexQueryUrl);

            if (_metadataOnly)
                pathBuilder.Append("&metadata-only=true");
            if (_indexEntriesOnly)
                pathBuilder.Append("&debug=entries");
            if (_includes != null && _includes.Count > 0)
            {
                pathBuilder.Append("&").Append(string.Join("&", _includes.Select(x => "include=" + x).ToArray()));
            }

            url = $"{node.Url}/databases/{node.Database}/" + pathBuilder;
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
                Result.DurationMilliseconds = -1;
        }

        public override bool IsReadRequest => true;
    }
}