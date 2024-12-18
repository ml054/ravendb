import React from "react";
import { PopoverWithHover } from "components/common/PopoverWithHover";
import { OngoingEtlTaskNodeInfo, OngoingTaskInfo } from "components/models/tasks";
import { NamedProgress, NamedProgressItem } from "components/common/NamedProgress";
import { Icon } from "components/common/Icon";
import { Button, Modal, ModalBody } from "reactstrap";
import useBoolean from "components/hooks/useBoolean";
import Code from "components/common/Code";
import copyToClipboard from "common/copyToClipboard";
import { NodeInfoFailure } from "components/pages/database/tasks/ongoingTasks/partials/NodeInfoFailure";

interface OngoingTaskEtlProgressTooltipProps {
    target: HTMLElement;
    nodeInfo: OngoingEtlTaskNodeInfo;
    task: OngoingTaskInfo;
    showPreview: (transformationName: string) => void;
}

export function OngoingEtlTaskProgressTooltip(props: OngoingTaskEtlProgressTooltipProps) {
    const { target, nodeInfo, showPreview } = props;

    if (nodeInfo.status === "failure") {
        return <NodeInfoFailure target={target} error={nodeInfo.details.error} />;
    }

    if (nodeInfo.status !== "success") {
        return null;
    }

    return (
        <PopoverWithHover rounded="true" target={target} placement="top">
            <div className="vstack gap-3 py-2">
                {nodeInfo.etlProgress &&
                    nodeInfo.etlProgress.map((transformationScriptProgress, index) => {
                        const nameNode = (
                            <div className="d-flex align-items-center justify-content-center gap-1">
                                {transformationScriptProgress.transformationName}
                                <Button
                                    color="link"
                                    className="p-0"
                                    size="xs"
                                    title="Show script preview"
                                    onClick={() => showPreview(transformationScriptProgress.transformationName)}
                                >
                                    <Icon icon="preview" margin="m-0" />
                                </Button>
                            </div>
                        );

                        return (
                            <div key={transformationScriptProgress.transformationName} className="vstack">
                                {transformationScriptProgress.transactionalId && (
                                    <div className="vstack">
                                        <div className="small-label d-flex align-items-center justify-content-center gap-1">
                                            Transactional Id
                                            <Button
                                                color="link"
                                                className="p-0"
                                                size="xs"
                                                onClick={() =>
                                                    copyToClipboard.copy(
                                                        transformationScriptProgress.transactionalId,
                                                        "Transactional Id was copied to clipboard."
                                                    )
                                                }
                                                title="Copy to clipboard"
                                            >
                                                <Icon icon="copy" margin="0" />
                                            </Button>
                                        </div>
                                        <small className="text-center mb-1">
                                            {transformationScriptProgress.transactionalId}
                                        </small>
                                    </div>
                                )}
                                <NamedProgress name={nameNode}>
                                    <NamedProgressItem progress={transformationScriptProgress.documents}>
                                        documents
                                    </NamedProgressItem>
                                    <NamedProgressItem progress={transformationScriptProgress.documentTombstones}>
                                        tombstones
                                    </NamedProgressItem>
                                    {transformationScriptProgress.counterGroups.total > 0 && (
                                        <NamedProgressItem progress={transformationScriptProgress.counterGroups}>
                                            counters
                                        </NamedProgressItem>
                                    )}
                                </NamedProgress>
                                {index !== nodeInfo.etlProgress.length - 1 && <hr className="mt-2 mb-0" />}
                            </div>
                        );
                    })}
            </div>
        </PopoverWithHover>
    );
}
