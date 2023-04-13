﻿import { loadStatus } from "./common";
import OngoingTaskState = Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskState;
import BackupType = Raven.Client.Documents.Operations.Backups.BackupType;

export interface OngoingTaskHubDefinitionSharedInfo extends OngoingTaskSharedInfo {
    delayReplicationTime: number;
    taskMode: Raven.Client.Documents.Operations.Replication.PullReplicationMode;
    hasFiltering: boolean;
}

interface Progress {
    total: number;
    processed: number;
}

export interface OngoingTaskNodeProgressDetails {
    global: Progress;
    documents: Progress;
    documentTombstones: Progress;
    counterGroups: Progress;
    transformationName: string;
    disabled: boolean;
    completed: boolean;
    processedPerSecond: number;
}

export interface OngoingTaskNodeInfoDetails {
    taskConnectionStatus: OngoingTaskConnectionStatus;
    responsibleNode: string;
    error: string;
}

export interface OngoingTaskNodeInfo<TNodeInfo extends OngoingTaskNodeInfoDetails = OngoingTaskNodeInfoDetails> {
    location: databaseLocationSpecifier;
    status: loadStatus;
    details: TNodeInfo;
}

export interface OngoingEtlTaskNodeInfo<TNodeInfo extends OngoingTaskNodeInfoDetails = OngoingTaskNodeInfoDetails>
    extends OngoingTaskNodeInfo<TNodeInfo> {
    etlProgress: OngoingTaskNodeProgressDetails[];
}

export type OngoingSubscriptionTaskNodeInfo = OngoingTaskNodeInfo<OngoingTaskSubscriptionNodeInfoDetails>;

export interface OngoingTaskSharedInfo {
    taskName: string;
    taskId: number;
    taskType: StudioTaskType;
    mentorNodeTag: string;
    responsibleNodeTag: string;
    taskState: OngoingTaskState;
    serverWide: boolean;
}

export interface OngoingTaskElasticSearchEtlSharedInfo extends OngoingTaskSharedInfo {
    connectionStringName: string;
    nodesUrls: string[];
}

export interface OngoingTaskExternalReplicationSharedInfo extends OngoingTaskSharedInfo {
    destinationDatabase: string;
    destinationUrl: string;
    connectionStringName: string;
    topologyDiscoveryUrls: string[];
    delayReplicationTime: number;
}

export interface OngoingTaskOlapEtlSharedInfo extends OngoingTaskSharedInfo {
    connectionStringName: string;
    destinationDescription: string;
    destinations: string[];
}

export interface OngoingTaskPeriodicBackupSharedInfo extends OngoingTaskSharedInfo {
    backupDestinations: string[];
    lastExecutingNodeTag: string;
    lastFullBackup: string;
    lastIncrementalBackup: string;
    backupType: BackupType;
    encrypted: boolean;
    nextBackup: Raven.Client.Documents.Operations.OngoingTasks.NextBackup;
    retentionPolicy: Raven.Client.Documents.Operations.Backups.RetentionPolicy;
}

export interface OngoingTaskRavenEtlSharedInfo extends OngoingTaskSharedInfo {
    destinationDatabase: string;
    destinationUrl: string;
    connectionStringName: string;
    topologyDiscoveryUrls: string[];
}

export interface OngoingTaskReplicationHubSharedInfo extends OngoingTaskSharedInfo {
    destinationDatabase: string;
    destinationUrl: string;
}

export interface OngoingTaskReplicationSinkSharedInfo extends OngoingTaskSharedInfo {
    destinationDatabase: string;
    destinationUrl: string;
    connectionStringName: string;
    topologyDiscoveryUrls: string[];
    hubName: string;
    mode: PullReplicationMode;
}

export type OngoingTaskKafkaEtlSharedInfo = OngoingTaskQueueEtlSharedInfo;

export type OngoingTaskRabbitMqEtlSharedInfo = OngoingTaskQueueEtlSharedInfo;

export interface OngoingTaskQueueEtlSharedInfo extends OngoingTaskSharedInfo {
    connectionStringName: string;
    url: string;
}

export interface OngoingTaskSqlEtlSharedInfo extends OngoingTaskSharedInfo {
    destinationServer: string;
    destinationDatabase: string;
    connectionStringName: string;
    connectionStringDefined: boolean;
}

export interface OngoingTaskSubscriptionSharedInfo extends OngoingTaskSharedInfo {
    changeVectorForNextBatchStartingPoint: string;
    changeVectorForNextBatchStartingPointPerShard: { [key: string]: string };
    lastBatchAckTime?: string;
    lastClientConnectionTime?: string;
}

export type OngoingTaskElasticSearchEtlNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskExternalReplicationNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskOlapEtlNodeInfoDetails = OngoingTaskNodeInfoDetails;

export interface OngoingTaskPeriodicBackupNodeInfoDetails extends OngoingTaskNodeInfoDetails {
    onGoingBackup: Raven.Client.Documents.Operations.OngoingTasks.RunningBackup;
}

export type OngoingTaskRavenEtlNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskReplicationHubNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskReplicationSinkNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskSqlEtlNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskSubscriptionNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskKafkaEtlNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type OngoingTaskRabbitMqEtlNodeInfoDetails = OngoingTaskNodeInfoDetails;

export type AnyEtlOngoingTaskInfo =
    | OngoingTaskSqlEtlInfo
    | OngoingTaskOlapEtlInfo
    | OngoingTaskElasticSearchEtlInfo
    | OngoingTaskRavenEtlInfo
    | OngoingTaskKafkaEtlInfo
    | OngoingTaskRabbitMqEtlInfo;

export interface OngoingTaskInfo<
    TSharded extends OngoingTaskSharedInfo = OngoingTaskSharedInfo,
    TNodesInfo extends OngoingTaskNodeInfo = OngoingTaskNodeInfo
> {
    shared: TSharded;
    nodesInfo: TNodesInfo[];
}

type OngoingTaskElasticSearchEtlInfo = OngoingTaskInfo<
    OngoingTaskElasticSearchEtlSharedInfo,
    OngoingEtlTaskNodeInfo<OngoingTaskElasticSearchEtlNodeInfoDetails>
>;

type OngoingTaskExternalReplicationInfo = OngoingTaskInfo<
    OngoingTaskExternalReplicationSharedInfo,
    OngoingTaskNodeInfo<OngoingTaskExternalReplicationNodeInfoDetails>
>;

type OngoingTaskOlapEtlInfo = OngoingTaskInfo<
    OngoingTaskOlapEtlSharedInfo,
    OngoingEtlTaskNodeInfo<OngoingTaskOlapEtlNodeInfoDetails>
>;

type OngoingTaskPeriodicBackupInfo = OngoingTaskInfo<
    OngoingTaskPeriodicBackupSharedInfo,
    OngoingTaskNodeInfo<OngoingTaskPeriodicBackupNodeInfoDetails>
>;

type OngoingTaskRavenEtlInfo = OngoingTaskInfo<
    OngoingTaskRavenEtlSharedInfo,
    OngoingEtlTaskNodeInfo<OngoingTaskRavenEtlNodeInfoDetails>
>;

type OngoingTaskReplicationHubInfo = OngoingTaskInfo<
    OngoingTaskReplicationHubSharedInfo,
    OngoingTaskNodeInfo<OngoingTaskReplicationHubNodeInfoDetails>
>;

type OngoingTaskHubDefinitionInfo = OngoingTaskInfo<OngoingTaskHubDefinitionSharedInfo, never>;

type OngoingTaskReplicationSinkInfo = OngoingTaskInfo<
    OngoingTaskReplicationSinkSharedInfo,
    OngoingTaskNodeInfo<OngoingTaskReplicationSinkNodeInfoDetails>
>;

type OngoingTaskSqlEtlInfo = OngoingTaskInfo<
    OngoingTaskSqlEtlSharedInfo,
    OngoingEtlTaskNodeInfo<OngoingTaskSqlEtlNodeInfoDetails>
>;

type OngoingTaskKafkaEtlInfo = OngoingTaskInfo<
    OngoingTaskKafkaEtlSharedInfo,
    OngoingEtlTaskNodeInfo<OngoingTaskKafkaEtlNodeInfoDetails>
>;

type OngoingTaskRabbitMqEtlInfo = OngoingTaskInfo<
    OngoingTaskRabbitMqEtlSharedInfo,
    OngoingEtlTaskNodeInfo<OngoingTaskRabbitMqEtlNodeInfoDetails>
>;

type OngoingTaskSubscriptionInfo = OngoingTaskInfo<
    OngoingTaskSubscriptionSharedInfo,
    OngoingTaskNodeInfo<OngoingTaskSubscriptionNodeInfoDetails>
>;
