/// <reference path="../../../../typings/tsd.d.ts" />

import appUrl from "common/appUrl";
import separatorMenuItem from "common/shell/menu/separatorMenuItem";
import database from "models/resources/database";

import getManageServerMenuItem from "common/shell/menu/items/manageServer";
import getDatabasesMenuItem from "common/shell/menu/items/databases";
import getSettingsMenuItem from "common/shell/menu/items/settings";
import getStatsMenuItem from "common/shell/menu/items/stats";
import getTasksMenuItem from "common/shell/menu/items/tasks";
import getIndexesMenuItem from "common/shell/menu/items/indexes";
import getDocumentsMenuItem from "common/shell/menu/items/documents";
import rootItems from "common/shell/menu/items/rootItems";

export = generateMenuItems;

function generateMenuItems(db: database) {
    if (!db) {
        return generateNoActiveDatabaseMenuItems();
    } 

    return generateActiveDatabaseMenuItems();
}

function generateNoActiveDatabaseMenuItems() {
    const appUrls = appUrl.forCurrentDatabase();
    return [
        new separatorMenuItem('Server'),
        getDatabasesMenuItem(appUrls),
        rootItems.dashboard(),
        rootItems.clusterDashboard(),
        getManageServerMenuItem(),
        rootItems.about()
    ];
}

function generateActiveDatabaseMenuItems() {
    const appUrls = appUrl.forCurrentDatabase();
    return [
        getDocumentsMenuItem(appUrls),
        getIndexesMenuItem(appUrls),        
        getTasksMenuItem(appUrls),
        getSettingsMenuItem(appUrls),
        getStatsMenuItem(appUrls),
        
        new separatorMenuItem('Server'),
        getDatabasesMenuItem(appUrls),
        rootItems.dashboard(),
        rootItems.clusterDashboard(),
        getManageServerMenuItem(),
        rootItems.about()
    ];
}



