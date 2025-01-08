import copyToClipboard from "common/copyToClipboard";
import changeVectorUtils from "common/changeVectorUtils";
import { Button, Input, InputGroup, Label } from "reactstrap";
import { Icon } from "components/common/Icon";
import React from "react";

interface ChangeVectorDetailsProps {
    sourceDatabaseChangeVector: string;
    lastAcceptedChangeVectorFromDestination: string;
}

export function ChangeVectorDetails(props: ChangeVectorDetailsProps) {
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

    if (!sourceDatabaseChangeVectorFormatted && !lastAcceptedChangeVectorFromDestination) {
        return null;
    }

    return (
        <div className="p-2">
            {sourceDatabaseChangeVectorFormatted && (
                <div className="mb-3">
                    <Label for="sourceDatabaseCv">Source database CV</Label>
                    <InputGroup>
                        <Input
                            type="text"
                            className="form-control"
                            id="sourceDatabaseCv"
                            readOnly
                            value={sourceDatabaseChangeVectorFormatted.map((x) => x.fullFormat).join(", ")}
                        />
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
                    </InputGroup>
                </div>
            )}

            {lastAcceptedChangeVectorFromDestinationFormatted && (
                <div className="mb-3">
                    <Label for="lastAcceptedCV">Last accepted CV (from destination)</Label>
                    <InputGroup>
                        <Input
                            type="text"
                            className="form-control"
                            id="lastAcceptedCV"
                            readOnly
                            value={lastAcceptedChangeVectorFromDestinationFormatted.map((x) => x.fullFormat).join(", ")}
                        />
                        <Button
                            onClick={() =>
                                handleCopyToClipboard(
                                    lastAcceptedChangeVectorFromDestinationFormatted.map((x) => x.fullFormat).join(", ")
                                )
                            }
                            color="primary"
                            size="sm"
                            title="Copy to clipboard"
                        >
                            <Icon icon="copy-to-clipboard" margin="m-0" />
                        </Button>
                    </InputGroup>
                </div>
            )}
        </div>
    );
}
