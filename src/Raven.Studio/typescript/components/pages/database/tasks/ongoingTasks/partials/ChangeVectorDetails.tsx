import copyToClipboard from "common/copyToClipboard";
import changeVectorUtils from "common/changeVectorUtils";
import { Button, Table } from "reactstrap";
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
