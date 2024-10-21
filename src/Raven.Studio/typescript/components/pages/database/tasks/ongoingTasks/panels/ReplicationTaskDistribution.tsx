import { OngoingTaskExternalReplicationInfo } from "components/models/tasks";

interface ReplicationTaskDistributionProps {
    task: OngoingTaskExternalReplicationInfo;
}
export function ReplicationTaskDistribution(props: ReplicationTaskDistributionProps) {
    //TODO:
    return (
        <div>
            <pre>{JSON.stringify(props.task, null, 2)}</pre>
        </div>
    );
}
