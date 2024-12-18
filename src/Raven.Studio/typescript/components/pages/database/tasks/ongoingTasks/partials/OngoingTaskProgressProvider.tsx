import useInterval from "hooks/useInterval";
import { useServices } from "hooks/useServices";
import EtlTaskProgress = Raven.Server.Documents.ETL.Stats.EtlTaskProgress;
import ReplicationTaskProgress = Raven.Server.Documents.Replication.Stats.ReplicationTaskProgress;
import useTimeout from "hooks/useTimeout";
import DatabaseUtils from "components/utils/DatabaseUtils";
import { databaseSelectors } from "components/common/shell/databaseSliceSelectors";
import { useAppSelector } from "components/store";
import InternalReplicationTaskProgress = Raven.Server.Documents.Replication.Stats.InternalReplicationTaskProgress;

interface OngoingTaskProgressProviderProps {
    onEtlProgress: (progress: EtlTaskProgress[], location: databaseLocationSpecifier) => void;
    onReplicationProgress: (progress: ReplicationTaskProgress[], location: databaseLocationSpecifier) => void;
    onInternalReplicationProgress: (
        progress: InternalReplicationTaskProgress[],
        location: databaseLocationSpecifier
    ) => void;
}

export function OngoingTaskProgressProvider(props: OngoingTaskProgressProviderProps): JSX.Element {
    const { onEtlProgress, onReplicationProgress, onInternalReplicationProgress } = props;
    const { tasksService } = useServices();

    const db = useAppSelector(databaseSelectors.activeDatabase);
    const locations = DatabaseUtils.getLocations(db);

    const loadProgress = () => {
        locations.forEach(async (location) => {
            const etlProgressTask = tasksService.getEtlProgress(db.name, location);
            const replicationProgressTask = tasksService.getReplicationProgress(db.name, location);
            const internalReplicationTask = tasksService.getInternalReplicationProgress(db.name, location);

            const errors: Error[] = [];

            try {
                const etlProgressResponse = await etlProgressTask;
                onEtlProgress(etlProgressResponse.Results, location);
            } catch (e) {
                errors.push(e);
            }

            try {
                const replicationProgressResponse = await replicationProgressTask;
                onReplicationProgress(replicationProgressResponse.Results, location);
            } catch (e) {
                errors.push(e);
            }

            try {
                const internalReplicationProgressResponse = await internalReplicationTask;
                onInternalReplicationProgress(internalReplicationProgressResponse.Results, location);
            } catch (e) {
                errors.push(e);
            }

            if (errors.length > 0) {
                errors.forEach(console.error);
                throw new Error("Unable to load progress");
            }
        });
    };

    useInterval(loadProgress, 8_000);
    useTimeout(loadProgress, 500);

    return null;
}
