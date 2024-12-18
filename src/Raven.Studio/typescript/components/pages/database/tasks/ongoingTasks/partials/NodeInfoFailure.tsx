import { PopoverWithHover } from "components/common/PopoverWithHover";
import { Icon } from "components/common/Icon";
import { Button, Modal, ModalBody } from "reactstrap";
import Code from "components/common/Code";
import copyToClipboard from "common/copyToClipboard";
import React from "react";
import useBoolean from "hooks/useBoolean";

interface NodeInfoFailureProps {
    target: HTMLElement;
    error: string;
}
export function NodeInfoFailure(props: NodeInfoFailureProps) {
    const { target, error } = props;

    const { value: isErrorModalOpen, toggle: toggleErrorModal } = useBoolean(false);

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
                            <code>{error}</code>
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
                    <Code code={error} language="csharp" />

                    <div className="text-end">
                        <Button
                            className="rounded-pill"
                            color="primary"
                            size="xs"
                            onClick={() => copyToClipboard.copy(error, "Copied error message to clipboard")}
                        >
                            <Icon icon="copy" /> <span>Copy to clipboard</span>
                        </Button>
                    </div>
                </ModalBody>
            </Modal>
        </>
    );
}
