/// <reference path="../../typings/tsd.d.ts"/>

import database = require("models/resources/database");
import activeDatabase = require("common/shell/activeDatabaseTracker");
import router = require("plugins/router");
import collection = require("models/database/documents/collection");
import messagePublisher = require("common/messagePublisher");
import databaseInfo = require("models/resources/info/databaseInfo");

class appUrl {

    static detectAppUrl() {
        const path = window.location.pathname.replace("\\", "/").replace("%5C", "/");
        const suffix = "studio/index.html";
        if (path.endsWith(suffix)) {
            return path.substring(0, path.length - suffix.length - 1);
        }
        return "";
    }

    static baseUrl = appUrl.detectAppUrl();

    private static currentDatabase = activeDatabase.default.database;
    
    // Stores some computed values that update whenever the current database updates.
    private static currentDbComputeds: computedAppUrls = {
        adminSettings: ko.pureComputed(() => appUrl.forAdminSettings()),
        adminSettingsCluster: ko.pureComputed(() => appUrl.forCluster()),

        hasApiKey: ko.pureComputed(() => appUrl.forHasApiKey()),

        databases: ko.pureComputed(() => appUrl.forDatabases()),
        manageDatabaseGroup: ko.pureComputed(() => appUrl.forManageDatabaseGroup(appUrl.currentDatabase())),
        documents: ko.pureComputed(() => appUrl.forDocuments(null, appUrl.currentDatabase())),
        conflicts: ko.pureComputed(() => appUrl.forConflicts(appUrl.currentDatabase())),
        patch: ko.pureComputed(() => appUrl.forPatch(appUrl.currentDatabase())),
        indexes: ko.pureComputed(() => appUrl.forIndexes(appUrl.currentDatabase())),
        megeSuggestions: ko.pureComputed(() => appUrl.forMegeSuggestions(appUrl.currentDatabase())),
        upgrade: ko.pureComputed(() => appUrl.forUpgrade(appUrl.currentDatabase())),
        transformers: ko.pureComputed(() => appUrl.forTransformers(appUrl.currentDatabase())),
        newIndex: ko.pureComputed(() => appUrl.forNewIndex(appUrl.currentDatabase())),
        editIndex: (indexName?: string) => ko.pureComputed(() => appUrl.forEditIndex(indexName, appUrl.currentDatabase())),
        newTransformer: ko.pureComputed(() => appUrl.forNewTransformer(appUrl.currentDatabase())),
        editTransformer: (transformerName?: string) => ko.pureComputed(() => appUrl.forEditTransformer(transformerName, appUrl.currentDatabase())),
        query: (indexName?: string) => ko.pureComputed(() => appUrl.forQuery(appUrl.currentDatabase(), indexName)),
        reporting: ko.pureComputed(() => appUrl.forReporting(appUrl.currentDatabase())),
        exploration: ko.pureComputed(() => appUrl.forExploration(appUrl.currentDatabase())),
        tasks: ko.pureComputed(() => appUrl.forTasks(appUrl.currentDatabase())),
        status: ko.pureComputed(() => appUrl.forStatus(appUrl.currentDatabase())),
        replicationPerfStats: ko.pureComputed(() => appUrl.forReplicationPerfStats(appUrl.currentDatabase())),
        sqlReplicationPerfStats: ko.pureComputed(() => appUrl.forSqlReplicationPerfStats(appUrl.currentDatabase())),

        ioStats: ko.pureComputed(() => appUrl.forIoStats(appUrl.currentDatabase())),

        requestsCount: ko.pureComputed(() => appUrl.forRequestsCount(appUrl.currentDatabase())),
        requestsTracing: ko.pureComputed(() => appUrl.forRequestsTracing(appUrl.currentDatabase())),
        indexPerformance: ko.pureComputed(() => appUrl.forIndexPerformance(appUrl.currentDatabase())),

        about: ko.pureComputed(() => appUrl.forAbout()),

        settings: ko.pureComputed(() => appUrl.forSettings(appUrl.currentDatabase())),
        logs: ko.pureComputed(() => appUrl.forLogs(appUrl.currentDatabase())),
        runningTasks: ko.pureComputed(() => appUrl.forRunningTasks(appUrl.currentDatabase())),
        alerts: ko.pureComputed(() => appUrl.forAlerts(appUrl.currentDatabase())),
        indexErrors: ko.pureComputed(() => appUrl.forIndexErrors(appUrl.currentDatabase())),
        replicationStats: ko.pureComputed(() => appUrl.forReplicationStats(appUrl.currentDatabase())),
        userInfo: ko.pureComputed(() => appUrl.forUserInfo(appUrl.currentDatabase())),
        visualizer: ko.pureComputed(() => appUrl.forVisualizer(appUrl.currentDatabase())),
        databaseSettings: ko.pureComputed(() => appUrl.forDatabaseSettings(appUrl.currentDatabase())),
        quotas: ko.pureComputed(() => appUrl.forQuotas(appUrl.currentDatabase())),
        periodicExport: ko.pureComputed(() => appUrl.forPeriodicExport(appUrl.currentDatabase())),
        replications: ko.pureComputed(() => appUrl.forReplications(appUrl.currentDatabase())),
        etl: ko.pureComputed(() => appUrl.forEtl(appUrl.currentDatabase())),
        hotSpare: ko.pureComputed(() => appUrl.forHotSpare()),
        versioning: ko.pureComputed(() => appUrl.forVersioning(appUrl.currentDatabase())),
        sqlReplications: ko.pureComputed(() => appUrl.forSqlReplications(appUrl.currentDatabase())),
        editSqlReplication: ko.pureComputed(() => appUrl.forEditSqlReplication(undefined, appUrl.currentDatabase())),
        sqlReplicationsConnections: ko.pureComputed(() => appUrl.forSqlReplicationConnections(appUrl.currentDatabase())),
        databaseStudioConfig: ko.pureComputed(() => appUrl.forDatabaseStudioConfig(appUrl.currentDatabase())),

        statusDebug: ko.pureComputed(() => appUrl.forStatusDebug(appUrl.currentDatabase())),
        statusDebugChanges: ko.pureComputed(() => appUrl.forStatusDebugChanges(appUrl.currentDatabase())),
        statusDebugMetrics: ko.pureComputed(() => appUrl.forStatusDebugMetrics(appUrl.currentDatabase())),
        statusDebugConfig: ko.pureComputed(() => appUrl.forStatusDebugConfig(appUrl.currentDatabase())),
        statusDebugDocrefs: ko.pureComputed(() => appUrl.forStatusDebugDocrefs(appUrl.currentDatabase())),
        statusDebugCurrentlyIndexing: ko.pureComputed(() => appUrl.forStatusDebugCurrentlyIndexing(appUrl.currentDatabase())),
        customFunctionsEditor: ko.pureComputed(() => appUrl.forCustomFunctionsEditor(appUrl.currentDatabase())),
        statusDebugQueries: ko.pureComputed(() => appUrl.forStatusDebugQueries(appUrl.currentDatabase())),
        statusDebugTasks: ko.pureComputed(() => appUrl.forStatusDebugTasks(appUrl.currentDatabase())),

        statusDebugRoutes: ko.pureComputed(() => appUrl.forStatusDebugRoutes(appUrl.currentDatabase())),
        statusDebugSqlReplication: ko.pureComputed(() => appUrl.forStatusDebugSqlReplication(appUrl.currentDatabase())),
        statusDebugIndexFields: ko.pureComputed(() => appUrl.forStatusDebugIndexFields(appUrl.currentDatabase())),
        statusDebugIdentities: ko.pureComputed(() => appUrl.forStatusDebugIdentities(appUrl.currentDatabase())),
        statusDebugWebSocket: ko.pureComputed(() => appUrl.forStatusDebugWebSocket(appUrl.currentDatabase())),
        statusDebugExplainReplication: ko.pureComputed(() => appUrl.forStatusDebugExplainReplication(appUrl.currentDatabase())),
        infoPackage: ko.pureComputed(() => appUrl.forInfoPackage(appUrl.currentDatabase())),

        subscriptions: ko.pureComputed(() => appUrl.forSubscriptions(appUrl.currentDatabase())),

        statusStorageStats: ko.pureComputed(() => appUrl.forStatusStorageStats(appUrl.currentDatabase())),
        statusStorageOnDisk: ko.pureComputed(() => appUrl.forStatusStorageOnDisk(appUrl.currentDatabase())),
        statusStorageBreakdown: ko.pureComputed(() => appUrl.forStatusStorageBreakdown(appUrl.currentDatabase())),
        statusStorageCollections: ko.pureComputed(() => appUrl.forStatusStorageCollections(appUrl.currentDatabase())),
        statusStorageReport: ko.pureComputed(() => appUrl.forStatusStorageReport(appUrl.currentDatabase())),

        isAreaActive: (routeRoot: string) => ko.pureComputed(() => appUrl.checkIsAreaActive(routeRoot)),
        isActive: (routeTitle: string) => ko.pureComputed(() => router.navigationModel().find(m => m.isActive() && m.title === routeTitle) != null),
        databasesManagement: ko.pureComputed(() => appUrl.forDatabases()),
        

    };

    static checkIsAreaActive(routeRoot: string): boolean {
        const items = router.routes.filter(m => m.isActive() && m.route != null && m.route != '');
        const isThereAny = items.some(m => (<string>m.route).substring(0, routeRoot.length) === routeRoot);
        return isThereAny;
    }

    static forUpgrade(db: database | databaseInfo) {
        return "#databases/upgrade?" + appUrl.getEncodedDbPart(db);
    }

    static forAdminSettings(): string {
        return "#admin/settings";
    }
    static forApiKeys(): string {
        return "#admin/settings/apiKeys";
    }

    static forCluster(): string {
        return "#admin/settings/cluster";
    }

    static forGlobalConfig(): string {
        return '#admin/settings/globalConfig';
    }

    static forServerSmugging(): string {
        return "#admin/settings/serverSmuggling";
    }

    static forGlobalConfigPeriodicExport(): string {
        return '#admin/settings/globalConfig';
    }

    static forGlobalConfigDatabaseSettings(): string {
        return '#admin/settings/globalConfigDatabaseSettings';
    }

    static forGlobalConfigReplication(): string {
        return '#admin/settings/globalConfigReplication';
    }

    static forGlobalConfigSqlReplication(): string {
        return "#admin/settings/globalConfigSqlReplication";
    }

    static forGlobalConfigQuotas(): string {
        return '#admin/settings/globalConfigQuotas';
    }

    static forGlobalConfigVersioning(): string {
        return "#admin/settings/globalConfigVersioning";
    }

    static forBackup(): string {
        return "#admin/settings/backup";
    }

    static forHotSpare(): string {
        return "#admin/settings/hotSpare";
    }

    static forCompact(): string {
        return "#admin/settings/compact";
    }

    static forRestore(): string {
        return "#admin/settings/restore";
    }

    static forAdminLogs(): string {
        return "#admin/settings/adminLogs";
    }

    static forServerTopology(): string { 
        return "#admin/settings/topology";
    }

    static forTrafficWatch(): string {
        return "#admin/settings/trafficWatch";
    }

    static forLicenseInformation(): string {
        return "#admin/settings/licenseInformation";
    }

    static forDebugInfo(): string {
        return "#admin/settings/debugInfo";
    }

    static forIoTest(): string {
        return "#admin/settings/ioTest";
    }

    static forDiskIoViewer(): string {
        return "#admin/settings/diskIoViewer";
    }

    static forAdminJsConsole(): string {
        return "#admin/settings/console";
    }

    static forStudioConfig(): string {
        return "#admin/settings/studioConfig";
    }

    static forDatabases(): string {
        return "#databases";
    }

    static forAbout(): string {
        return "#about";
    }

    static forHasApiKey(): string {
        return "#has-api-key";
    }

    static forEditDoc(id: string, db: database | databaseInfo): string {
        const databaseUrlPart = appUrl.getEncodedDbPart(db);
        const docIdUrlPart = id ? "&id=" + encodeURIComponent(id) : "";
        return "#databases/edit?" + docIdUrlPart + databaseUrlPart;
    }

    static forViewDocumentAtRevision(id: string, revisionEtag: number, db: database | databaseInfo): string {
        const databaseUrlPart = appUrl.getEncodedDbPart(db);
        const docIdUrlPart = "&id=" + encodeURIComponent(id) + "&revision=" + revisionEtag;
        return "#databases/edit?" + docIdUrlPart + databaseUrlPart;
    }

    static forEditItem(itemId: string, db: database | databaseInfo, itemIndex: number, collectionName?: string): string {
        const urlPart = appUrl.getEncodedDbPart(db);
        const itemIdUrlPart = itemId ? "&id=" + encodeURIComponent(itemId) : "";

        const pagedListInfo = collectionName && itemIndex != null ? "&list=" + encodeURIComponent(collectionName) + "&item=" + itemIndex : "";
        const databaseTag = "#databases";       
        return databaseTag + "/edit?" + itemIdUrlPart + urlPart + pagedListInfo;
    }

    static forEditQueryItem(itemNumber: number, res: database | databaseInfo, index: string, query?: string, sort?:string): string {
        const databaseUrlPart = appUrl.getEncodedDbPart(res);
        const indexUrlPart = "&index=" + index;
        const itemNumberUrlPart = "&item=" + itemNumber;
        const queryInfoUrlPart = query? "&query=" + encodeURIComponent(query): "";
        const sortInfoUrlPart = sort?"&sorts=" + sort:"";
        const dbTag = "#databases";
        return dbTag + "/edit?" + databaseUrlPart + indexUrlPart + itemNumberUrlPart + queryInfoUrlPart + sortInfoUrlPart;
    }

    static forNewDoc(db: database | databaseInfo, collection: string = null): string {
        const databaseUrlPart = appUrl.getEncodedDbPart(db);
        let url = "#databases/edit?" + databaseUrlPart;
        if (collection) {
            url += "&new=" + encodeURIComponent(collection);
        }
        return url;
    }


    /**
    * Gets the URL for status page.
    * @param database The database to use in the URL. If null, the current database will be used.
    */
    static forStatus(db: database | databaseInfo): string {
        return "#databases/status?" + appUrl.getEncodedDbPart(db);
    }

    static forReplicationPerfStats(db: database | databaseInfo): string {
        return "#databases/status/replicationPerfStats?" + appUrl.getEncodedDbPart(db);
    }

    static forSqlReplicationPerfStats(db: database | databaseInfo): string {
        return "#databases/status/sqlReplicationPerfStats?" + appUrl.getEncodedDbPart(db);
    }

    static forIoStats(db: database | databaseInfo): string {
        return "#databases/status/ioStats?" + appUrl.getEncodedDbPart(db);
    }

    static forRequestsCount(db: database | databaseInfo): string {
        return "#databases/status/requests?" + appUrl.getEncodedDbPart(db);
    }

    static forRequestsTracing(db: database | databaseInfo): string {
        return "#databases/status/requests/tracing?" + appUrl.getEncodedDbPart(db);
    }

    static forIndexPerformance(db: database | databaseInfo, indexName?: string): string {
        return `#databases/indexes/performance?${(appUrl.getEncodedDbPart(db))}&${appUrl.getEncodedIndexNamePart(indexName)}`;
    }

    static forStatusDebug(db: database | databaseInfo): string {
        return "#databases/status/debug?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugChanges(db: database | databaseInfo): string {
        return "#databases/status/debug?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugMetrics(db: database | databaseInfo): string {
        return "#databases/status/debug/metrics?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugConfig(db: database | databaseInfo): string {
        return "#databases/status/debug/config?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugDocrefs(db: database | databaseInfo): string {
        return "#databases/status/debug/docrefs?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugCurrentlyIndexing(db: database | databaseInfo): string {
        return "#databases/status/debug/currentlyIndexing?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugQueries(db: database | databaseInfo): string {
        return "#databases/status/debug/queries?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugTasks(db: database | databaseInfo): string {
        return "#databases/status/debug/tasks?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugRoutes(db: database | databaseInfo): string {
        return "#databases/status/debug/routes?" + appUrl.getEncodedDbPart(db);
    }

    static forRequestTracing(db: database | databaseInfo): string {
        return "#databases/status/requests/tracking?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugSqlReplication(db: database | databaseInfo): string {
        return "#databases/status/debug/sqlReplication?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugIndexFields(db: database | databaseInfo): string {
        return "#databases/status/debug/indexFields?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugIdentities(db: database | databaseInfo): string {
        return "#databases/status/debug/identities?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugWebSocket(db: database | databaseInfo): string {
        return "#databases/status/debug/webSocket?" + appUrl.getEncodedDbPart(db);
    }

    static forStatusDebugExplainReplication(db: database | databaseInfo): string {
        return "#databases/status/debug/explainReplication?" + appUrl.getEncodedDbPart(db);
    }

    static forInfoPackage(db: database | databaseInfo): string {
        return '#databases/status/infoPackage?' + appUrl.getEncodedDbPart(db);
    }

    static forSubscriptions(db: database | databaseInfo): string {
        return '#databases/status/subscriptions?' + appUrl.getEncodedDbPart(db); 
    }

    static forStatusStorageOnDisk(db: database | databaseInfo): string {
        return '#databases/status/storage?' + appUrl.getEncodedDbPart(db);
    }

    static forStatusStorageStats(db: database | databaseInfo): string {
        return '#databases/status/storage/stats?' + appUrl.getEncodedDbPart(db);
    }

    static forStatusStorageBreakdown(db: database | databaseInfo): string {
        return '#databases/status/storage/storageBreakdown?' + appUrl.getEncodedDbPart(db);
    }

    static forStatusStorageCollections(db: database | databaseInfo): string {
        return '#databases/status/storage/collections?' + appUrl.getEncodedDbPart(db);
    }

    static forStatusStorageReport(db: database | databaseInfo): string {
        return '#databases/status/storage/report?' + appUrl.getEncodedDbPart(db);
    }

    static forSettings(db: database | databaseInfo): string {
        return "#databases/settings/databaseSettings?" + appUrl.getEncodedDbPart(db);
    }
    
    static forLogs(db: database | databaseInfo): string {
        return "#databases/status/logs?" + appUrl.getEncodedDbPart(db);
    }

    static forRunningTasks(db: database | databaseInfo): string {
        return "#databases/status/runningTasks?" + appUrl.getEncodedDbPart(db);
    }

    static forAlerts(db: database | databaseInfo): string {
        return "#databases/status/alerts?" + appUrl.getEncodedDbPart(db);
    }

    static forIndexErrors(db: database | databaseInfo): string {
        return "#databases/indexes/indexErrors?" + appUrl.getEncodedDbPart(db);
    }

    static forReplicationStats(db: database | databaseInfo): string {
        return "#databases/status/replicationStats?" + appUrl.getEncodedDbPart(db);
    }

    static forUserInfo(db: database | databaseInfo): string {
        return "#databases/status/userInfo?" + appUrl.getEncodedDbPart(db);
    }

    static forVisualizer(db: database | databaseInfo, index: string = null): string {
        let url = "#databases/indexes/visualizer?" + appUrl.getEncodedDbPart(db);
        if (index) { 
            url += "&index=" + index;
        }
        return url;
    }

    static forDatabaseSettings(db: database | databaseInfo): string {
        return "#databases/settings/databaseSettings?" + appUrl.getEncodedDbPart(db);
    }

    static forQuotas(db: database | databaseInfo): string {
        return "#databases/settings/quotas?" + appUrl.getEncodedDbPart(db);
    }

    static forPeriodicExport(db: database | databaseInfo): string {
        return "#databases/settings/periodicExport?" + appUrl.getEncodedDbPart(db);
    }

    static forReplications(db: database | databaseInfo): string {
        return "#databases/settings/replication?" + appUrl.getEncodedDbPart(db);
    }

    static forEtl(db: database | databaseInfo): string {
        return "#databases/settings/etl?" + appUrl.getEncodedDbPart(db);
    }

    static forVersioning(db: database | databaseInfo): string {
        return "#databases/settings/versioning?" + appUrl.getEncodedDbPart(db);
    }

    static forSqlReplications(db: database | databaseInfo): string {
        return "#databases/settings/sqlReplication?" + appUrl.getEncodedDbPart(db);
    }

    static forEditSqlReplication(sqlReplicationName: string, db: database | databaseInfo):string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/settings/editSqlReplication/" + encodeURIComponent(sqlReplicationName) + "?" + databasePart;
    }

    static forSqlReplicationConnections(db: database | databaseInfo): string {
        return "#databases/settings/sqlReplicationConnectionStringsManagement?" + appUrl.getEncodedDbPart(db);
    }

    static forDatabaseStudioConfig(db: database | databaseInfo): string {
        return "#databases/settings/databaseStudioConfig?" + appUrl.getEncodedDbPart(db);
    }

    static forManageDatabaseGroup(db: database | databaseInfo): string {
        return "#databases/manageDatabaseGroup?" + appUrl.getEncodedDbPart(db);
    }

    static forDocuments(collectionName: string, db: database | databaseInfo): string {
        if (collectionName === "All Documents")
            collectionName = null;

        const collectionPart = collectionName ? "collection=" + encodeURIComponent(collectionName) : "";
        const  databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/documents?" + collectionPart + databasePart;
    }

    static forDocumentsByDatabaseName(collection: string, dbName: string): string {
        const collectionPart = collection ? "collection=" + encodeURIComponent(collection) : "";
        return "#/databases/documents?" + collectionPart + "&database=" + encodeURIComponent(dbName);;
    }

    static forConflicts(db: database | databaseInfo, documentId?: string): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        const docIdUrlPart = documentId ? "&id=" + encodeURIComponent(documentId) : "";
        return "#databases/replicationEtl/conflicts?" + databasePart + docIdUrlPart;
    }

    static forPatch(db: database | databaseInfo, hashOfRecentPatch?: number): string {
        const databasePart = appUrl.getEncodedDbPart(db);

        if (hashOfRecentPatch) {
            const patchPath = "recentpatch-" + hashOfRecentPatch;
            return "#databases/patch/" + encodeURIComponent(patchPath) + "?" + databasePart;
        } else {
            return "#databases/patch?" + databasePart;    
        }
    }

    static forIndexes(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/indexes?" + databasePart;
    }

    static forNewIndex(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/indexes/edit?" + databasePart;
    }

    static forEditIndex(indexName: string, db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/indexes/edit/" + encodeURIComponent(indexName) + "?" + databasePart;
    }

    static forNewTransformer(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/transformers/edit?" + databasePart;
    }

    static forEditTransformer(transformerName: string, db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/transformers/edit/" + encodeURIComponent(transformerName) + "?" + databasePart;
    }

    static forTransformers(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/transformers?" + databasePart;
    }

    static forQuery(db: database | databaseInfo, indexNameOrHashToQuery?: string | number): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        let indexToQueryComponent = indexNameOrHashToQuery as string;
        if (typeof indexNameOrHashToQuery === "number") {
            indexToQueryComponent = "recentquery-" + indexNameOrHashToQuery;
        } 

        const indexPart = indexToQueryComponent ? "/" + encodeURIComponent(indexToQueryComponent) : "";
        return "#databases/query/index" + indexPart + "?" + databasePart;
    }

    static forReporting(db: database | databaseInfo, indexName?: string): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        const indexPart = indexName ? "/" + encodeURIComponent(indexName) : "";
        return "#databases/query/reporting" + indexPart + "?" + databasePart;
    }

    static forExploration(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/query/exploration?" + databasePart;
    }

    static forTasks(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/tasks?" + databasePart;
    }

    static forDatabaseQuery(db: database | databaseInfo): string {
        if (db) {
            return appUrl.baseUrl + "/databases/" + db.name;
        }

        return this.baseUrl;
    }

    static forTerms(indexName: string, db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/indexes/terms/" + encodeURIComponent(indexName) + "?" + databasePart;
    }

    static forMegeSuggestions(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/indexes/mergeSuggestions?" + databasePart;
    }

    static forImportDatabase(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/tasks/importDatabase?" + databasePart;
    }

    static forExportDatabase(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/tasks/exportDatabase?" + databasePart;
    }

    static forExportCollectionCsv(collection: collection, db: database | databaseInfo, customColumns?: string[]): string {
        if (collection.isAllDocuments || collection.isSystemDocuments) {
            return null;
        }
        var args = {
            format: "excel",
            download: true,
            query: "Tag:" + collection.name,
            column: customColumns
        }

        //TODO: we don't have Raven/DocumentsByEntityName anymore
        return appUrl.forDatabaseQuery(db) + "/streams/query/Raven/DocumentsByEntityName" + appUrl.urlEncodeArgs(args);
    }

    static forCustomFunctionsEditor(db: database | databaseInfo): string {
        return "#databases/settings/customFunctionsEditor?" + appUrl.getEncodedDbPart(db);
    }

    static forOngoingTasks(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/tasks/ongoingTasks?" + databasePart;
    }

    static forSampleData(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/tasks/sampleData?" + databasePart;
    }

    static forCsvImport(db: database | databaseInfo): string {
        const databasePart = appUrl.getEncodedDbPart(db);
        return "#databases/tasks/csvImport?" + databasePart;
    }

    static forIndexesRawData(db: database | databaseInfo): string {
        return window.location.protocol + "//" + window.location.host + "/databases/" + db.name + "/indexes";
    }

    static forIndexQueryRawData(db: database | databaseInfo, indexName:string){
        return window.location.protocol + "//" + window.location.host + "/databases/" + db.name + "/indexes/" + indexName;
    }

    static forReportingRawData(db: database | databaseInfo, indexName: string) {
        return window.location.protocol + "//" + window.location.host + "/databases/" + db.name + "/facets/" + indexName;
    }

    static forTransformersRawData(db: database | databaseInfo): string {
        return window.location.protocol + "//" + window.location.host + "/databases/" + db.name + "/transformers";
    }

    static forDatabasesRawData(): string {
        return window.location.protocol + "//" + window.location.host + "/databases";
    }

    static forDocumentRawData(db: database | databaseInfo, docId:string): string {
        return window.location.protocol + "//" + window.location.host + "/databases/" + db.name + "/docs?id=" + docId;
    }

    static getDatabaseNameFromUrl(): string {
        const indicator = "database=";
        const hash = window.location.hash;
        const index = hash.indexOf(indicator);
        if (index >= 0) {
            let segmentEnd = hash.indexOf("&", index);
            if (segmentEnd === -1) {
                segmentEnd = hash.length;
            }

            const databaseName = hash.substring(index + indicator.length, segmentEnd);
            return decodeURIComponent(databaseName);
        } else {
            return null;
        } 
    }

    /**
    * Gets the server URL.
    */
    static forServer() {
        return window.location.protocol + "//" + window.location.host + appUrl.baseUrl;
    }

    /**
    * Gets the address for the current page but for the specified database.
    */
    static forCurrentPage(db: database) {
        const routerInstruction = router.activeInstruction();
        if (routerInstruction) {

            let currentDatabaseName: string = null;
            const dbInUrl = routerInstruction.queryParams[database.type];
            if (dbInUrl) {
                currentDatabaseName = dbInUrl;
            }

            const isDifferentDatabaseInAddress = !currentDatabaseName || currentDatabaseName !== db.name.toLowerCase();
            if (isDifferentDatabaseInAddress) {
                const existingAddress = window.location.hash;
                const existingQueryString = currentDatabaseName ? "database=" + encodeURIComponent(currentDatabaseName) : null;
                const newQueryString = "database=" + encodeURIComponent(db.name);
                return existingQueryString ?
                    existingAddress.replace(existingQueryString, newQueryString) :
                    existingAddress + (window.location.hash.indexOf("?") >= 0 ? "&" : "?") + db.type + "=" + encodeURIComponent(db.name);
            }
        }
    }

    static forCurrentDatabase(): computedAppUrls {
        return appUrl.currentDbComputeds;
    }

    private static getEncodedDbPart(db?: database | databaseInfo) {
        return db ? "&database=" + encodeURIComponent(db.name) : "";
    }
    
    private static getEncodedIndexNamePart(indexName?: string) {
        return indexName ? "indexName=" + encodeURIComponent(indexName) : "";
    }

    static mapUnknownRoutes(router: DurandalRouter) {
        router.mapUnknownRoutes((instruction: DurandalRouteInstruction) => {
            const queryString = !!instruction.queryString ? ("?" + instruction.queryString) : "";

            if (instruction.fragment === "has-api-key" || instruction.fragment.startsWith("api-key")) {
                // reload page to reinitialize shell and properly consume/provide OAuth token
                location.reload();
            } else {
                messagePublisher.reportError("Unknown route", "The route " + instruction.fragment + queryString + " doesn't exist, redirecting...");

                const fragment = instruction.fragment;
                const appUrls = appUrl.currentDbComputeds;
                location.href = fragment.startsWith("admin/settings") ? appUrls.adminSettings() : appUrls.databasesManagement();
            }
        });
    }

    static toExternalUrl(serverUrl: string, localLink: string) {
        return serverUrl + "/studio/index.html" + localLink;
    }

    static urlEncodeArgs(args: any): string {
        const propNameAndValues: Array<string> = [];
        for (let prop of Object.keys(args)) {
            const value = args[prop];

            if (value instanceof Array) {
                for (let i = 0; i < value.length; i++) {
                    propNameAndValues.push(prop + "=" + encodeURIComponent(value[i]));
                }
            } else if (value !== undefined) {
                propNameAndValues.push(prop + "=" + encodeURIComponent(value));
            }
        }

        return "?" + propNameAndValues.join("&");
    }
}

export = appUrl;
