﻿using System;
using System.Net.Http;
using Raven.Client.Documents.Identity;
using Raven.Client.Http;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Commands
{
    public class NextHiLoCommand : RavenCommand<HiLoResult>
    {
        private readonly string _tag;
        private readonly long _lastBatchSize;
        private readonly DateTime _lastRangeAt;
        private readonly string _identityPartsSeparator;
        private readonly long _lastRangeMax;

        public NextHiLoCommand(string tag, long lastBatchSize, DateTime lastRangeAt, string identityPartsSeparator, long lastRangeMax)
        {
            _tag = tag ?? throw new ArgumentNullException(nameof(tag));
            _lastBatchSize = lastBatchSize;
            _lastRangeAt = lastRangeAt;
            _identityPartsSeparator = identityPartsSeparator ?? throw new ArgumentNullException(nameof(identityPartsSeparator));
            _lastRangeMax = lastRangeMax;
        }

        public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
        {
            var path = $"hilo/next?tag={_tag}&lastBatchSize={_lastBatchSize}&lastRangeAt={_lastRangeAt:o}&identityPartsSeparator={_identityPartsSeparator}&lastMax={_lastRangeMax}";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
            };

            url = $"{node.Url}/databases/{node.Database}/" + path;
            return request;
        }

        public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
        {
            Result = JsonDeserializationClient.HiLoResult(response);
        }

        public override bool IsReadRequest => true;
    }
}
