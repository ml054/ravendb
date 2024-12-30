import React from "react";
import { PopoverWithHover } from "components/common/PopoverWithHover";
import { OngoingTaskNodeReplicationProgressDetails } from "components/models/tasks";
import { NamedProgress, NamedProgressItem } from "components/common/NamedProgress";
import { ChangeVectorDetails } from "components/pages/database/tasks/ongoingTasks/partials/ChangeVectorDetails";
import { NodeInfoFailure } from "components/pages/database/tasks/ongoingTasks/partials/NodeInfoFailure";
import { loadStatus } from "components/models/common";

interface ReplicationTaskProgressTooltipProps {
    target: HTMLElement;
    status: loadStatus;
    error: string;
    progress: OngoingTaskNodeReplicationProgressDetails[];
    sourceDatabaseChangeVector: string;
    lastAcceptedChangeVectorFromDestination: string;
}

export function ReplicationTaskProgressTooltip(props: ReplicationTaskProgressTooltipProps) {
    const { target, progress, status, error, sourceDatabaseChangeVector, lastAcceptedChangeVectorFromDestination } =
        props;

    if (status === "failure") {
        return <NodeInfoFailure target={target} error={error} />;
    }

    if (status !== "success") {
        return null;
    }

    const hasAnyDetailsToShow =
        progress?.length > 0 || sourceDatabaseChangeVector || lastAcceptedChangeVectorFromDestination;

    if (!hasAnyDetailsToShow) {
        return null;
    }

    //TODO: format this tooltip

    return (
        <PopoverWithHover rounded="true" target={target} placement="top">
            <div className="vstack gap-3 py-2">
                <ChangeVectorDetails
                    sourceDatabaseChangeVector={sourceDatabaseChangeVector}
                    lastAcceptedChangeVectorFromDestination={lastAcceptedChangeVectorFromDestination}
                />
                {progress &&
                    progress.map((singleProgress, index) => {
                        return (
                            <div key={"progress-" + index} className="vstack">
                                <NamedProgress name="Replication">
                                    <NamedProgressItem progress={singleProgress.documents}>documents</NamedProgressItem>
                                    <NamedProgressItem progress={singleProgress.documentTombstones}>
                                        tombstones
                                    </NamedProgressItem>
                                    <NamedProgressItem progress={singleProgress.revisions}>revisions</NamedProgressItem>
                                    <NamedProgressItem progress={singleProgress.attachments}>
                                        attachments
                                    </NamedProgressItem>
                                    <NamedProgressItem progress={singleProgress.counterGroups}>
                                        counters
                                    </NamedProgressItem>
                                    <NamedProgressItem progress={singleProgress.timeSeries}>
                                        time-series
                                    </NamedProgressItem>
                                    <NamedProgressItem progress={singleProgress.timeSeriesDeletedRanges}>
                                        time-series deleted ranges
                                    </NamedProgressItem>
                                </NamedProgress>
                                {index !== progress.length - 1 && <hr className="mt-2 mb-0" />}
                            </div>
                        );
                    })}
            </div>
        </PopoverWithHover>
    );
}
