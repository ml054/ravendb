import { OngoingTaskExternalReplicationInfo, OngoingTaskReplicationHubInfo } from "components/models/tasks";

interface ReplicationTaskDistributionProps {
    task: OngoingTaskExternalReplicationInfo | OngoingTaskReplicationHubInfo;
}
export function ReplicationTaskDistribution(props: ReplicationTaskDistributionProps) {
    const { task } = props;

    return (
        <div>
            ReplicationTaskDistribution
            <pre>{JSON.stringify(props.task, null, 2)}</pre>
        </div>
    );
}
