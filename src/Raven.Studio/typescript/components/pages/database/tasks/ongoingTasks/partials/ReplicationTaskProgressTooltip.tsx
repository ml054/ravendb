import React from "react";
import { PopoverWithHover } from "components/common/PopoverWithHover";
import {
    OngoingReplicationProgressAwareTaskNodeInfo,
    OngoingTaskAbstractReplicationNodeInfoDetails,
} from "components/models/tasks";
import { NamedProgress, NamedProgressItem } from "components/common/NamedProgress";
import { ChangeVectorDetails } from "components/pages/database/tasks/ongoingTasks/partials/ChangeVectorDetails";
import { NodeInfoFailure } from "components/pages/database/tasks/ongoingTasks/partials/NodeInfoFailure";

interface ReplicationTaskProgressTooltipProps {
    target: HTMLElement;
    nodeInfo: OngoingReplicationProgressAwareTaskNodeInfo<OngoingTaskAbstractReplicationNodeInfoDetails>;
}

export function ReplicationTaskProgressTooltip(props: ReplicationTaskProgressTooltipProps) {
    const { target, nodeInfo } = props;

    if (nodeInfo.status === "failure") {
        return <NodeInfoFailure target={target} error={nodeInfo.details.error} />;
    }

    if (nodeInfo.status !== "success") {
        return null;
    }

    const hasAnyDetailsToShow =
        nodeInfo.progress?.length > 0 ||
        nodeInfo.details?.sourceDatabaseChangeVector ||
        nodeInfo.details?.lastAcceptedChangeVectorFromDestination;

    if (!hasAnyDetailsToShow) {
        return null;
    }

    //TODO: format this tooltip

    return (
        <PopoverWithHover rounded="true" target={target} placement="top">
            <div className="vstack gap-3 py-2">
                <ChangeVectorDetails
                    sourceDatabaseChangeVector={nodeInfo.details?.sourceDatabaseChangeVector}
                    lastAcceptedChangeVectorFromDestination={nodeInfo.details?.lastAcceptedChangeVectorFromDestination}
                />
                {nodeInfo.progress &&
                    nodeInfo.progress.map((progress, index) => {
                        return (
                            <div key={"progress-" + index} className="vstack">
                                <NamedProgress name="Replication">
                                    <NamedProgressItem progress={progress.documents}>documents</NamedProgressItem>
                                    <NamedProgressItem progress={progress.documentTombstones}>
                                        tombstones
                                    </NamedProgressItem>
                                    <NamedProgressItem progress={progress.revisions}>revisions</NamedProgressItem>
                                    <NamedProgressItem progress={progress.attachments}>attachments</NamedProgressItem>
                                    <NamedProgressItem progress={progress.counterGroups}>counters</NamedProgressItem>
                                    <NamedProgressItem progress={progress.timeSeries}>time-series</NamedProgressItem>
                                    <NamedProgressItem progress={progress.timeSeriesDeletedRanges}>
                                        time-series deleted ranges
                                    </NamedProgressItem>
                                </NamedProgress>
                                {index !== nodeInfo.progress.length - 1 && <hr className="mt-2 mb-0" />}
                            </div>
                        );
                    })}
            </div>
        </PopoverWithHover>
    );
}
