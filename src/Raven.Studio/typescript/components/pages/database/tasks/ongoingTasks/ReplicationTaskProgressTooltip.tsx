import React from "react";
import { PopoverWithHover } from "components/common/PopoverWithHover";
import {
    OngoingReplicationProgressAwareTaskNodeInfo,
    OngoingTaskAbstractReplicationNodeInfoDetails,
    OngoingTaskInfo,
} from "components/models/tasks";
import { NamedProgress, NamedProgressItem } from "components/common/NamedProgress";
import { Icon } from "components/common/Icon";
import { Button, Modal, ModalBody, Table } from "reactstrap";
import useBoolean from "components/hooks/useBoolean";
import Code from "components/common/Code";
import copyToClipboard from "common/copyToClipboard";
import changeVectorUtils from "common/changeVectorUtils";

interface OngoingReplicationTaskProgressTooltipProps {
    target: HTMLElement;
    nodeInfo: OngoingReplicationProgressAwareTaskNodeInfo<OngoingTaskAbstractReplicationNodeInfoDetails>;
    task: OngoingTaskInfo;
}

export function ReplicationTaskProgressTooltip(props: OngoingReplicationTaskProgressTooltipProps) {
    const { target, nodeInfo } = props;
    const { value: isErrorModalOpen, toggle: toggleErrorModal } = useBoolean(false);

    if (nodeInfo.status === "failure") {
        return (
            <>
                {!isErrorModalOpen && (
                    <PopoverWithHover target={target} placement="top">
                        <div className="vstack gap-2 p-3">
                            <div className="text-danger">
                                <Icon icon="warning" color="danger" /> Unable to load task status:
                            </div>
                            <div
                                className="overflow-auto"
                                style={{
                                    maxWidth: "300px",
                                    maxHeight: "300px",
                                }}
                            >
                                <code>{nodeInfo.details.error}</code>
                            </div>
                            <Button size="sm" onClick={toggleErrorModal}>
                                Open error in modal <Icon icon="newtab" margin="ms-1" />
                            </Button>
                        </div>
                    </PopoverWithHover>
                )}
                <Modal
                    size="xl"
                    wrapClassName="bs5"
                    isOpen={isErrorModalOpen}
                    toggle={toggleErrorModal}
                    contentClassName="modal-border bulge-danger"
                >
                    <ModalBody>
                        <div className="position-absolute m-2 end-0 top-0">
                            <Button close onClick={toggleErrorModal} />
                        </div>
                        <div className="hstack gap-3 mb-4">
                            <div className="text-center">
                                <Icon icon="warning" color="danger" className="fs-1" margin="m-0" />
                            </div>
                            <div className="text-center lead">Unable to load task status:</div>
                        </div>
                        <Code code={nodeInfo.details.error} language="csharp" />

                        <div className="text-end">
                            <Button
                                className="rounded-pill"
                                color="primary"
                                size="xs"
                                onClick={() =>
                                    copyToClipboard.copy(nodeInfo.details.error, "Copied error message to clipboard")
                                }
                            >
                                <Icon icon="copy" /> <span>Copy to clipboard</span>
                            </Button>
                        </div>
                    </ModalBody>
                </Modal>
            </>
        );
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
    return (
        <PopoverWithHover rounded="true" target={target} placement="top">
            <div className="vstack gap-3 py-2">
                <ChangeVectorDetails
                    sourceDatabaseChangeVector={nodeInfo.details.sourceDatabaseChangeVector}
                    lastAcceptedChangeVectorFromDestination={nodeInfo.details.lastAcceptedChangeVectorFromDestination}
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

interface ChangeVectorDetailsProps {
    sourceDatabaseChangeVector: string;
    lastAcceptedChangeVectorFromDestination: string;
}

function ChangeVectorDetails(props: ChangeVectorDetailsProps) {
    const { sourceDatabaseChangeVector, lastAcceptedChangeVectorFromDestination } = props;

    const handleCopyToClipboard = (value: string) => {
        copyToClipboard.copy(value, "Item has been copied to clipboard");
    };

    const sourceDatabaseChangeVectorFormatted = sourceDatabaseChangeVector
        ? changeVectorUtils.formatChangeVector(sourceDatabaseChangeVector, true)
        : null;
    const lastAcceptedChangeVectorFromDestinationFormatted = lastAcceptedChangeVectorFromDestination
        ? changeVectorUtils.formatChangeVector(lastAcceptedChangeVectorFromDestination, true)
        : null;

    return (
        <div className="p-2">
            <Table>
                <tbody>
                    {sourceDatabaseChangeVectorFormatted && (
                        <tr>
                            <td>Source database CV</td>
                            <td>
                                <div className="d-flex gap-1">
                                    <div>
                                        {sourceDatabaseChangeVectorFormatted.map((x) => x.shortFormat).join(", ")}
                                    </div>
                                    <Button
                                        onClick={() =>
                                            handleCopyToClipboard(
                                                sourceDatabaseChangeVectorFormatted.map((x) => x.fullFormat).join(",")
                                            )
                                        }
                                        color="primary"
                                        size="sm"
                                        title="Copy to clipboard"
                                    >
                                        <Icon icon="copy-to-clipboard" margin="m-0" />
                                    </Button>
                                </div>
                            </td>
                        </tr>
                    )}

                    {lastAcceptedChangeVectorFromDestinationFormatted && (
                        <tr>
                            <td>Last accepted CV (from destination)</td>
                            <td>
                                <div className="d-flex gap-1">
                                    <div>
                                        {lastAcceptedChangeVectorFromDestinationFormatted
                                            .map((x) => x.shortFormat)
                                            .join(", ")}
                                    </div>
                                    <Button
                                        onClick={() =>
                                            handleCopyToClipboard(
                                                lastAcceptedChangeVectorFromDestinationFormatted
                                                    .map((x) => x.fullFormat)
                                                    .join(",")
                                            )
                                        }
                                        color="primary"
                                        size="sm"
                                        title="Copy to clipboard"
                                    >
                                        <Icon icon="copy-to-clipboard" margin="m-0" />
                                    </Button>
                                </div>
                            </td>
                        </tr>
                    )}
                </tbody>
            </Table>
        </div>
    );
}
