﻿using System;
using System.Linq;
using System.Net.Http;
using Raven.Client.Documents.Conventions;
using Raven.Client.Http;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Operations.Indexes
{
    public class GetTermsOperation : IAdminOperation<string[]>
    {
        private readonly string _indexName;
        private readonly string _field;
        private readonly string _fromValue;
        private readonly int? _pageSize;

        public GetTermsOperation(string indexName, string field, string fromValue, int? pageSize = null)
        {
            _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _fromValue = fromValue;
            _pageSize = pageSize;
        }

        public RavenCommand<string[]> GetCommand(DocumentConventions conventions, JsonOperationContext context)
        {
            return new GetTermsCommand(_indexName, _field, _fromValue, _pageSize);
        }

        private class GetTermsCommand : RavenCommand<string[]>
        {
            private readonly string _indexName;
            private readonly string _field;
            private readonly string _fromValue;
            private readonly int? _pageSize;

            public GetTermsCommand(string indexName, string field, string fromValue, int? pageSize)
            {
                _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
                _field = field ?? throw new ArgumentNullException(nameof(field));
                _fromValue = fromValue;
                _pageSize = pageSize;
            }

            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/indexes/terms?name={Uri.EscapeDataString(_indexName)}&field={Uri.EscapeDataString(_field)}&fromValue={_fromValue}&pageSize={_pageSize}";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                var result = JsonDeserializationClient.TermsQueryResult(response);
                var terms = result.Terms;

                Result = terms.ToArray();
            }

            public override bool IsReadRequest => true;
        }
    }
}