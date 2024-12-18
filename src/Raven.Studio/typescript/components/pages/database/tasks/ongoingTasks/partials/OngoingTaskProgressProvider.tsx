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
            const etlProgressResponse = await tasksService.getEtlProgress(db.name, location);
            onEtlProgress(etlProgressResponse.Results, location);
        });

        locations.forEach(async (location) => {
            const replicationProgressResponse = await tasksService.getReplicationProgress(db.name, location);
            onReplicationProgress(replicationProgressResponse.Results, location);
        });

        locations.forEach(async (location) => {
            const internalReplicationProgressResponse = await tasksService.getInternalReplicationProgress(
                db.name,
                location
            );
            onInternalReplicationProgress(internalReplicationProgressResponse.Results, location);
        });
    };

    useInterval(loadProgress, 8_000);
    useTimeout(loadProgress, 500);

    return null;
}
