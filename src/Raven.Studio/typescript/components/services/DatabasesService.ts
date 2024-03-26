/// <reference path="../../../typings/tsd.d.ts" />

import saveDatabaseLockModeCommand from "commands/resources/saveDatabaseLockModeCommand";
import DatabaseLockMode = Raven.Client.ServerWide.DatabaseLockMode;
import getEssentialDatabaseStatsCommand from "commands/resources/getEssentialDatabaseStatsCommand";
import getDatabaseDetailedStatsCommand from "commands/resources/getDatabaseDetailedStatsCommand";
import deleteDatabaseFromNodeCommand from "commands/resources/deleteDatabaseFromNodeCommand";
import toggleDynamicNodeAssignmentCommand from "commands/database/dbGroup/toggleDynamicNodeAssignmentCommand";
import reorderNodesInDatabaseGroupCommand = require("commands/database/dbGroup/reorderNodesInDatabaseGroupCommand");
import deleteOrchestratorFromNodeCommand from "commands/resources/deleteOrchestratorFromNodeCommand";
import deleteDatabaseCommand from "commands/resources/deleteDatabaseCommand";
import toggleDatabaseCommand from "commands/resources/toggleDatabaseCommand";
import getDatabasesStateForStudioCommand from "commands/resources/getDatabasesStateForStudioCommand";
import getDatabaseStateForStudioCommand from "commands/resources/getDatabaseStateForStudioCommand";
import restartDatabaseCommand = require("commands/resources/restartDatabaseCommand");
import getDatabaseStudioConfigurationCommand = require("commands/resources/getDatabaseStudioConfigurationCommand");
import StudioConfiguration = Raven.Client.Documents.Operations.Configuration.StudioConfiguration;
import saveDatabaseStudioConfigurationCommand = require("commands/resources/saveDatabaseStudioConfigurationCommand");
import getNextOperationIdCommand = require("commands/database/studio/getNextOperationIdCommand");
import killOperationCommand = require("commands/operations/killOperationCommand");
import getRefreshConfigurationCommand = require("commands/database/documents/getRefreshConfigurationCommand");
import saveRefreshConfigurationCommand = require("commands/database/documents/saveRefreshConfigurationCommand");
import RefreshConfiguration = Raven.Client.Documents.Operations.Refresh.RefreshConfiguration;
import ExpirationConfiguration = Raven.Client.Documents.Operations.Expiration.ExpirationConfiguration;
import saveExpirationConfigurationCommand = require("commands/database/documents/saveExpirationConfigurationCommand");
import getExpirationConfigurationCommand = require("commands/database/documents/getExpirationConfigurationCommand");
import DataArchivalConfiguration = Raven.Client.Documents.Operations.DataArchival.DataArchivalConfiguration;
import getDataArchivalConfigurationCommand from "commands/database/documents/getDataArchivalConfigurationCommand";
import saveDataArchivalConfigurationCommand from "commands/database/documents/saveDataArchivalConfigurationCommand";
import getTombstonesStateCommand = require("commands/database/debug/getTombstonesStateCommand");
import forceTombstonesCleanupCommand = require("commands/database/debug/forceTombstonesCleanupCommand");
import RevisionsConfiguration = Raven.Client.Documents.Operations.Revisions.RevisionsConfiguration;
import RevisionsCollectionConfiguration = Raven.Client.Documents.Operations.Revisions.RevisionsCollectionConfiguration;
import getRevisionsForConflictsConfigurationCommand = require("commands/database/documents/getRevisionsForConflictsConfigurationCommand");
import getRevisionsConfigurationCommand = require("commands/database/documents/getRevisionsConfigurationCommand");
import saveRevisionsConfigurationCommand = require("commands/database/documents/saveRevisionsConfigurationCommand");
import saveRevisionsForConflictsConfigurationCommand = require("commands/database/documents/saveRevisionsForConflictsConfigurationCommand");
import enforceRevisionsConfigurationCommand = require("commands/database/settings/enforceRevisionsConfigurationCommand");
import getCustomSortersCommand = require("commands/database/settings/getCustomSortersCommand");
import deleteCustomSorterCommand = require("commands/database/settings/deleteCustomSorterCommand");
import deleteCustomAnalyzerCommand = require("commands/database/settings/deleteCustomAnalyzerCommand");
import getCustomAnalyzersCommand = require("commands/database/settings/getCustomAnalyzersCommand");
import getDocumentsCompressionConfigurationCommand = require("commands/database/documents/getDocumentsCompressionConfigurationCommand");
import saveDocumentsCompressionCommand = require("commands/database/documents/saveDocumentsCompressionCommand");
import promoteDatabaseNodeCommand = require("commands/database/debug/promoteDatabaseNodeCommand");
import revertRevisionsCommand = require("commands/database/documents/revertRevisionsCommand");
import getConflictSolverConfigurationCommand = require("commands/database/documents/getConflictSolverConfigurationCommand");
import testSqlConnectionStringCommand = require("commands/database/cluster/testSqlConnectionStringCommand");
import testRabbitMqServerConnectionCommand = require("commands/database/cluster/testRabbitMqServerConnectionCommand");
import testKafkaServerConnectionCommand = require("commands/database/cluster/testKafkaServerConnectionCommand");
import testElasticSearchNodeConnectionCommand = require("commands/database/cluster/testElasticSearchNodeConnectionCommand");
import getDatabaseRecordCommand = require("commands/resources/getDatabaseRecordCommand");
import saveDatabaseRecordCommand = require("commands/resources/saveDatabaseRecordCommand");
import saveConflictSolverConfigurationCommand = require("commands/database/documents/saveConflictSolverConfigurationCommand");
import testClusterNodeConnectionCommand = require("commands/database/cluster/testClusterNodeConnectionCommand");
import deleteConnectionStringCommand = require("commands/database/settings/deleteConnectionStringCommand");
import getConnectionStringsCommand = require("commands/database/settings/getConnectionStringsCommand");
import saveConnectionStringCommand = require("commands/database/settings/saveConnectionStringCommand");
import { ConnectionStringDto } from "components/pages/database/settings/connectionStrings/connectionStringsTypes";
import testAzureQueueStorageServerConnectionCommand from "commands/database/cluster/testAzureQueueStorageServerConnectionCommand";
import saveCustomSorterCommand = require("commands/database/settings/saveCustomSorterCommand");
import queryCommand = require("commands/database/query/queryCommand");

export default class DatabasesService {
    async setLockMode(databaseNames: string[], newLockMode: DatabaseLockMode) {
        return new saveDatabaseLockModeCommand(databaseNames, newLockMode).execute();
    }

    async toggle(databaseNames: string[], enable: boolean) {
        return new toggleDatabaseCommand(databaseNames, enable).execute();
    }

    async deleteDatabase(toDelete: string[], hardDelete: boolean) {
        return new deleteDatabaseCommand(toDelete, hardDelete).execute();
    }

    async getEssentialStats(databaseName: string) {
        return new getEssentialDatabaseStatsCommand(databaseName).execute();
    }

    async getDatabasesState(targetNodeTag: string) {
        return new getDatabasesStateForStudioCommand(targetNodeTag).execute();
    }

    async getDatabaseState(targetNodeTag: string, databaseName: string) {
        return new getDatabaseStateForStudioCommand(targetNodeTag, databaseName).execute();
    }

    async getDetailedStats(databaseName: string, location: databaseLocationSpecifier) {
        return new getDatabaseDetailedStatsCommand(databaseName, location).execute();
    }

    async deleteDatabaseFromNode(databaseName: string, nodes: string[], hardDelete: boolean) {
        return new deleteDatabaseFromNodeCommand(databaseName, nodes, hardDelete).execute();
    }

    async deleteOrchestratorFromNode(databaseName: string, node: string) {
        return new deleteOrchestratorFromNodeCommand(databaseName, node).execute();
    }

    async toggleDynamicNodeAssignment(databaseName: string, enabled: boolean) {
        return new toggleDynamicNodeAssignmentCommand(databaseName, enabled).execute();
    }

    async reorderNodesInGroup(databaseName: string, tagsOrder: string[], fixOrder: boolean) {
        return new reorderNodesInDatabaseGroupCommand(databaseName, tagsOrder, fixOrder).execute();
    }

    async restartDatabase(databaseName: string, location: databaseLocationSpecifier) {
        return new restartDatabaseCommand(databaseName, location).execute();
    }

    async getDatabaseStudioConfiguration(databaseName: string) {
        return new getDatabaseStudioConfigurationCommand(databaseName).execute();
    }

    async saveDatabaseStudioConfiguration(dto: Partial<StudioConfiguration>, databaseName: string) {
        return new saveDatabaseStudioConfigurationCommand(dto, databaseName).execute();
    }

    async getNextOperationId(databaseName: string) {
        return new getNextOperationIdCommand(databaseName).execute();
    }

    async killOperation(databaseName: string, taskId: number) {
        return new killOperationCommand(databaseName, taskId).execute();
    }

    async getRefreshConfiguration(databaseName: string) {
        return new getRefreshConfigurationCommand(databaseName).execute();
    }

    async saveRefreshConfiguration(databaseName: string, dto: RefreshConfiguration) {
        return new saveRefreshConfigurationCommand(databaseName, dto).execute();
    }

    async getDataArchivalConfiguration(databaseName: string) {
        return new getDataArchivalConfigurationCommand(databaseName).execute();
    }

    async saveDataArchivalConfiguration(databaseName: string, dto: DataArchivalConfiguration): Promise<any> {
        return new saveDataArchivalConfigurationCommand(databaseName, dto).execute();
    }

    async getExpirationConfiguration(databaseName: string) {
        return new getExpirationConfigurationCommand(databaseName).execute();
    }

    async saveExpirationConfiguration(databaseName: string, dto: ExpirationConfiguration) {
        return new saveExpirationConfigurationCommand(databaseName, dto).execute();
    }

    async getTombstonesState(databaseName: string, location: databaseLocationSpecifier) {
        return new getTombstonesStateCommand(databaseName, location).execute();
    }

    async forceTombstonesCleanup(databaseName: string, location: databaseLocationSpecifier) {
        return new forceTombstonesCleanupCommand(databaseName, location).execute();
    }

    async getRevisionsForConflictsConfiguration(databaseName: string) {
        return new getRevisionsForConflictsConfigurationCommand(databaseName).execute();
    }

    async saveRevisionsForConflictsConfiguration(databaseName: string, dto: RevisionsCollectionConfiguration) {
        return new saveRevisionsForConflictsConfigurationCommand(databaseName, dto).execute();
    }

    async getRevisionsConfiguration(databaseName: string) {
        return new getRevisionsConfigurationCommand(databaseName).execute();
    }

    async saveRevisionsConfiguration(databaseName: string, dto: RevisionsConfiguration) {
        return new saveRevisionsConfigurationCommand(databaseName, dto).execute();
    }

    async enforceRevisionsConfiguration(
        databaseName: string,
        includeForceCreated = false,
        collections: string[] = null
    ) {
        return new enforceRevisionsConfigurationCommand(databaseName, includeForceCreated, collections).execute();
    }

    async revertRevisions(databaseName: string, dto: Raven.Server.Documents.Revisions.RevertRevisionsRequest) {
        return new revertRevisionsCommand(dto, databaseName).execute();
    }

    async getCustomAnalyzers(databaseName: string, getNamesOnly = false) {
        return new getCustomAnalyzersCommand(databaseName, getNamesOnly).execute();
    }

    async deleteCustomAnalyzer(databaseName: string, name: string) {
        return new deleteCustomAnalyzerCommand(databaseName, name).execute();
    }

    async getCustomSorters(databaseName: string) {
        return new getCustomSortersCommand(databaseName).execute();
    }

    async deleteCustomSorter(databaseName: string, name: string) {
        return new deleteCustomSorterCommand(databaseName, name).execute();
    }

    async saveCustomSorter(...args: ConstructorParameters<typeof saveCustomSorterCommand>) {
        return new saveCustomSorterCommand(...args).execute();
    }

    async getDocumentsCompressionConfiguration(databaseName: string) {
        return new getDocumentsCompressionConfigurationCommand(databaseName).execute();
    }

    async saveDocumentsCompression(
        databaseName: string,
        dto: Raven.Client.ServerWide.DocumentsCompressionConfiguration
    ) {
        return new saveDocumentsCompressionCommand(databaseName, dto).execute();
    }

    async promoteDatabaseNode(databaseName: string, nodeTag: string) {
        return new promoteDatabaseNodeCommand(databaseName, nodeTag).execute();
    }

    async getConflictSolverConfiguration(databaseName: string) {
        return new getConflictSolverConfigurationCommand(databaseName).execute();
    }

    async saveConflictSolverConfiguration(databaseName: string, dto: Raven.Client.ServerWide.ConflictSolver) {
        return new saveConflictSolverConfigurationCommand(databaseName, dto).execute();
    }

    async getConnectionStrings(databaseName: string) {
        return new getConnectionStringsCommand(databaseName).execute();
    }

    async saveConnectionString(databaseName: string, connectionString: ConnectionStringDto) {
        return new saveConnectionStringCommand(databaseName, connectionString).execute();
    }

    async deleteConnectionString(
        databaseName: string,
        type: Raven.Client.Documents.Operations.ETL.EtlType,
        connectionStringName: string
    ) {
        return new deleteConnectionStringCommand(databaseName, type, connectionStringName).execute();
    }

    async testClusterNodeConnection(serverUrl: string, databaseName?: string, bidirectional = true) {
        return new testClusterNodeConnectionCommand(serverUrl, databaseName, bidirectional).execute();
    }

    async testSqlConnectionString(databaseName: string, connectionString: string, factoryName: string) {
        return new testSqlConnectionStringCommand(databaseName, connectionString, factoryName).execute();
    }

    async testRabbitMqServerConnection(databaseName: string, connectionString: string) {
        return new testRabbitMqServerConnectionCommand(databaseName, connectionString).execute();
    }

    async testAzureQueueStorageServerConnection(db: database, connectionString: string) {
        return new testAzureQueueStorageServerConnectionCommand(db, connectionString).execute();
    }

    async testKafkaServerConnection(
        databaseName: string,
        bootstrapServers: string,
        useServerCertificate: boolean,
        connectionOptionsDto: {
            [optionKey: string]: string;
        }
    ) {
        return new testKafkaServerConnectionCommand(
            databaseName,
            bootstrapServers,
            useServerCertificate,
            connectionOptionsDto
        ).execute();
    }

    async testElasticSearchNodeConnection(
        databaseName: string,
        serverUrl: string,
        authenticationDto: Raven.Client.Documents.Operations.ETL.ElasticSearch.Authentication
    ) {
        return new testElasticSearchNodeConnectionCommand(databaseName, serverUrl, authenticationDto).execute();
    }

    async getDatabaseRecord(databaseName: string, reportRefreshProgress = false) {
        return new getDatabaseRecordCommand(databaseName, reportRefreshProgress).execute();
    }

    async saveDatabaseRecord(databaseName: string, databaseRecord: documentDto, etag: number) {
        return new saveDatabaseRecordCommand(databaseName, databaseRecord, etag).execute();
    }

    async query(...args: ConstructorParameters<typeof queryCommand>) {
        return new queryCommand(...args).execute();
    }
}
