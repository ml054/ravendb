import { Button, Modal, ModalBody } from "reactstrap";
import { Icon } from "components/common/Icon";
import Code from "components/common/Code";
import copyToClipboard from "common/copyToClipboard";
import React from "react";

interface ErrorModalProps {
    toggleErrorModal: () => void;
    error: string;
}

export function ErrorModal(props: ErrorModalProps) {
    const { toggleErrorModal, error } = props;
    return (
        <Modal
            size="xl"
            wrapClassName="bs5"
            isOpen
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
                    <div className="text-center lead">Error:</div>
                </div>
                <Code code={error} language="csharp" />

                <div className="text-end mt-3">
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
    );
}
