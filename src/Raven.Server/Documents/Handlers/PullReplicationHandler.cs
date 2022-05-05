using System.Net;
using System.Threading.Tasks;
using Raven.Client.Documents.Operations.OngoingTasks;
using Raven.Client.Documents.Operations.Replication;
using Raven.Client.Exceptions;
using Raven.Client.Util;
using Raven.Server.Documents.Handlers.Processors.Replication;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Documents.Handlers
{
    public class PullReplicationHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/admin/tasks/pull-replication/hub", "PUT", AuthorizationStatus.DatabaseAdmin)]
        public async Task DefineHub()
        {
            using (var processor = new PullReplicationHandlerProcessorForDefineHub(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/tasks/pull-replication/hub/access", "PUT", AuthorizationStatus.DatabaseAdmin)]
        public async Task RegisterHubAccess()
        {
            using (var processor = new PullReplicationHandlerProcessorForRegisterHubAccess(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/tasks/pull-replication/hub/access", "DELETE", AuthorizationStatus.DatabaseAdmin)]
        public async Task UnregisterHubAccess()
        {
            using (var processor = new PullReplicationHandlerProcessorForUnregisterHubAccess(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/tasks/pull-replication/hub/access", "GET", AuthorizationStatus.DatabaseAdmin)]
        public async Task ListHubAccess()
        {
            using (var processor = new PullReplicationHandlerProcessorForGetListHubAccess(this))
                await processor.ExecuteAsync();
        }

        [RavenAction("/databases/*/admin/tasks/sink-pull-replication", "POST", AuthorizationStatus.DatabaseAdmin)]
        public async Task UpdatePullReplicationOnSinkNode()
        {
            if (ResourceNameValidator.IsValidResourceName(Database.Name, ServerStore.Configuration.Core.DataDirectory.FullPath, out string errorMessage) == false)
                throw new BadRequestException(errorMessage);

            ServerStore.LicenseManager.AssertCanAddPullReplicationAsSink();

            using (ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            {
                PullReplicationAsSink pullReplication = null;
                await DatabaseConfigurations(
                    (_, databaseName, blittableJson, guid) => ServerStore.UpdatePullReplicationAsSink(databaseName, blittableJson, guid, out pullReplication),
                    "update-sink-pull-replication", GetRaftRequestIdFromQuery(),
                    fillJson: (json, _, index) =>
                    {
                        using (context.OpenReadTransaction())
                        {
                            var topology = ServerStore.Cluster.ReadDatabaseTopology(context, Database.Name);
                            json[nameof(OngoingTask.ResponsibleNode)] = Database.WhoseTaskIsIt(topology, pullReplication, null);
                        }

                        json[nameof(ModifyOngoingTaskResult.TaskId)] = pullReplication.TaskId == 0 ? index : pullReplication.TaskId;
                    }, statusCode: HttpStatusCode.Created);
            }
        }

        [RavenAction("/databases/*/admin/pull-replication/generate-certificate", "POST", AuthorizationStatus.DatabaseAdmin, DisableOnCpuCreditsExhaustion = true)]
        public async Task GeneratePullReplicationCertificate()
        {
            using (var processor = new PullReplicationHandlerProcessorForGenerateCertificate<DatabaseRequestHandler>(this))
                await processor.ExecuteAsync();
        }

        public class PullReplicationCertificate
        {
            public string PublicKey { get; set; }
            public string Certificate { get; set; }
            public string Thumbprint { get; set; }
        }
    }
}
