import { InputItemLimit } from "components/models/common";
import { UncontrolledPopover } from "reactstrap";
import genUtils from "common/generalUtils";

interface ToggleLimitBadgeProps {
    target: string;
    count: number;
    limit: InputItemLimit;
}

export default function ToggleLimitBadge({ target, count, limit }: ToggleLimitBadgeProps) {
    return (
        <>
            <span className={`multi-toggle-item-count text-dark bg-${limit.badgeColor ?? "warning"}`}>
                {genUtils.formatNumberToStringFixed(count, 0)} / {genUtils.formatNumberToStringFixed(limit.value, 0)}
            </span>
            {limit.message && (
                <UncontrolledPopover target={target} trigger="hover" placement="top" className="bs5">
                    <div className="p-2">{limit.message}</div>
                </UncontrolledPopover>
            )}
        </>
    );
}
