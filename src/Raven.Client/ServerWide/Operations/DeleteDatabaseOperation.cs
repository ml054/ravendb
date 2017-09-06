using System;
using System.Net.Http;
using Raven.Client.Documents.Conventions;
using Raven.Client.Http;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.ServerWide.Operations
{
    public class DeleteDatabaseOperation : IServerOperation<DeleteDatabaseResult>
    {
        private readonly string _name;
        private readonly bool _hardDelete;
        private readonly string _fromNode;
        private readonly int _timeInSec;

        public DeleteDatabaseOperation(string name, bool hardDelete,string fromNode = null, int timeInSec = 0)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _hardDelete = hardDelete;
            _fromNode = fromNode;
            _timeInSec = timeInSec;
        }

        public RavenCommand<DeleteDatabaseResult> GetCommand(DocumentConventions conventions, JsonOperationContext ctx)
        {
            return new DeleteDatabaseCommand(_name, _hardDelete, _fromNode, _timeInSec);
        }

        private class DeleteDatabaseCommand : RavenCommand<DeleteDatabaseResult>
        {
            private readonly string _name;
            private readonly bool _hardDelete;
            private readonly string _fromNode;
            private readonly int _timeInSec;

            public DeleteDatabaseCommand(string name, bool hardDelete,string fromNode, int timeInSec)
            {
                _name = name ?? throw new ArgumentNullException(nameof(name));
                _hardDelete = hardDelete;
                _fromNode = fromNode;
                _timeInSec = timeInSec;
                ResponseType = RavenCommandResponseType.Object;
            }

            public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
            {
                url = $"{node.Url}/admin/databases?name={_name}";
                if (_hardDelete)
                {
                    url += "&hard-delete=true";
                }
                if (string.IsNullOrEmpty(_fromNode) == false)
                {
                    url += $"&from-node={_fromNode}";
                }
                if (_timeInSec > 0)
                {
                    url += $"&confirmationTimeoutInSec={_timeInSec}";
                }
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete
                };

                return request;
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                Result = JsonDeserializationClient.DeleteDatabaseResult(response);
            }
            
            public override bool IsReadRequest => false;
        }
    }
}
