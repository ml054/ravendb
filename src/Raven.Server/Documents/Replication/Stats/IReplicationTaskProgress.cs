using System.Collections.Generic;
using Raven.Client.Documents.Replication;

namespace Raven.Server.Documents.Replication.Stats
{
    public interface IReplicationTaskProgress
    {
        public string TaskName { get; set; }
        public ReplicationNode.ReplicationType ReplicationType { get; set; }
        public List<ReplicationProcessProgress> ProcessesProgress { get; set; }
    }
}
