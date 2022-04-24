using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raven.Server.Documents.Sharding.Handlers.Processors.Replication;
using Raven.Server.Routing;

namespace Raven.Server.Documents.Sharding.Handlers
{
    public class ShardedPullReplicationHandler : ShardedDatabaseRequestHandler
    {
        [RavenShardedAction("/databases/*/admin/tasks/pull-replication/hub", "PUT")]
        public async Task DefineHub()
        {
            using (var processor = new ShardedPullReplicationHandlerProcessorForDefineHub(this))
                await processor.ExecuteAsync();
        }
    }
}
