﻿using System.Net.Http;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Session;
using Raven.Client.Http;
using Sparrow.Json;

namespace Raven.Client.ServerWide.Operations
{
    public class GetDatabaseRecordOperation : IServerOperation<DatabaseRecord>
    {
        private readonly string _database;

        public GetDatabaseRecordOperation(string database)
        {
            _database = database;
        }

        public RavenCommand<DatabaseRecord> GetCommand(DocumentConventions conventions, JsonOperationContext ctx)
        {
            return new GetDatabaseRecordCommand(_database);
        }
    }


    public class GetDatabaseRecordCommand : RavenCommand<DatabaseRecord>
    {
        private readonly string _database;
        private readonly DocumentConventions _conventions = new DocumentConventions();

        public override bool IsReadRequest => false;

        public GetDatabaseRecordCommand(string database)
        {
            _database = database;
        }

        public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
        {
            url = $"{node.Url}/admin/databases?name={_database}";
            return new HttpRequestMessage
            {
                Method = HttpMethod.Get
            };
        }

        public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
        {
            if (response == null)
            {
                Result = null;
                return;
            }

            Result = (DatabaseRecord)EntityToBlittable.ConvertToEntity(typeof(DatabaseRecord), "database-record", response, _conventions);
        }
    }
}
