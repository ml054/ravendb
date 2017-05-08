﻿import intermediateMenuItem = require("common/shell/menu/intermediateMenuItem");
import leafMenuItem = require("common/shell/menu/leafMenuItem");

export = getSettingsMenuItem;

function getSettingsMenuItem(appUrls: computedAppUrls) {
    var items: menuItem[] = [
        new leafMenuItem({
            route: ['databases/settings', 'databases/settings/databaseSettings'],
            moduleId: 'viewmodels/database/settings/databaseSettings',
            title: 'Database Settings',
            nav: true,
            css: 'icon-database-settings',
            dynamicHash: appUrls.databaseSettings
        }),
        /* TODO
        new leafMenuItem({
            route: 'databases/settings/quotas',
            moduleId: 'viewmodels/database/settings/quotas',
            title: 'Quotas',
            nav: true,
            css: 'icon-plus',
            dynamicHash: appUrls.quotas
        }),*/
        new leafMenuItem({
            route: 'databases/settings/replication',
            moduleId: 'viewmodels/database/settings/replications',
            title: 'Replication',
            nav: true,
            css: 'icon-replication',
            dynamicHash: appUrls.replications
        }),
        /* TODO
        new leafMenuItem({
            route: 'databases/settings/etl',
            moduleId: 'viewmodels/database/settings/etl',
            title: 'ETL',
            nav: true,
            css: 'icon-plus',
            dynamicHash: appUrls.etl
        }),*/
        /* TODO
        new leafMenuItem({
            route: 'databases/settings/sqlReplication',
            moduleId: 'viewmodels/database/settings/sqlReplications',
            title: 'SQL Replication',
            nav: true,
            css: 'icon-sql-replication',
            dynamicHash: appUrls.sqlReplications
        }),*/
        new leafMenuItem({
            route: 'databases/settings/editSqlReplication(/:sqlReplicationName)',
            moduleId: 'viewmodels/database/settings/editSqlReplication',
            title: 'Edit SQL Replication',
            nav: false,
            css: 'icon-sql-replication',
            dynamicHash: appUrls.editSqlReplication
        }),
        new leafMenuItem({
            route: 'databases/settings/sqlReplicationConnectionStringsManagement',
            moduleId: 'viewmodels/database/settings/sqlReplicationConnectionStringsManagement',
            title: 'SQL Replication Connection Strings',
            nav: false,
            css: 'icon-sql-replication-connection-string',
            dynamicHash: appUrls.sqlReplicationsConnections
        }),
        new leafMenuItem({
            route: 'databases/settings/versioning',
            moduleId: 'viewmodels/database/settings/versioning',
            title: 'Versioning',
            nav: true,
            css: 'icon-versioning',
            dynamicHash: appUrls.versioning
        }),
        new leafMenuItem({
            route: 'databases/settings/periodicExport',
            moduleId: 'viewmodels/database/settings/periodicExport',
            title: 'Periodic Export',
            nav: true,
            css: 'icon-periodic-export',
            dynamicHash: appUrls.periodicExport
        }),
        new leafMenuItem({
            route: 'databases/settings/customFunctionsEditor',
            moduleId: 'viewmodels/database/settings/customFunctionsEditor',
            title: 'Custom Functions',
            nav: true,
            css: 'icon-custom-functions',
            dynamicHash: appUrls.customFunctionsEditor
        })
        /*TODO
        new leafMenuItem({
            route: 'databases/settings/databaseStudioConfig',
            moduleId: 'viewmodels/databaseStudioConfig',
            title: 'Studio Config',
            nav: true,
            css: 'icon-studio-config',
            dynamicHash: appUrls.databaseStudioConfig
        })*/
    ];

    return new intermediateMenuItem('Settings', items, 'icon-settings');
}
