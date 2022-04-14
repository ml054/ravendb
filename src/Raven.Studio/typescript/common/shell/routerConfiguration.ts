import appUrl from "common/appUrl";
import intermediateMenuItem from "common/shell/menu/intermediateMenuItem";
import leafMenuItem from "common/shell/menu/leafMenuItem";

import getManageServerMenuItem from "common/shell/menu/items/manageServer";
import getDatabasesMenuItem from "common/shell/menu/items/databases";
import getSettingsMenuItem from "common/shell/menu/items/settings";
import getStatsMenuItem from "common/shell/menu/items/stats";
import getTasksMenuItem from "common/shell/menu/items/tasks";
import getIndexesMenuItem from "common/shell/menu/items/indexes";
import getDocumentsMenuItem from "common/shell/menu/items/documents";
import rootItems from "common/shell/menu/items/rootItems";

export = getRouterConfiguration();

function getRouterConfiguration(): Array<DurandalRouteConfiguration> {
    return generateAllMenuItems()
        .map(getMenuItemDurandalRoutes)
        .reduce((result, next) => result.concat(next), [])
        .reduce((result: any[], next: any) => {
            const nextJson = JSON.stringify(next);
            if (!result.some(x => JSON.stringify(x) === nextJson)) {
                result.push(next);
            }

            return result;
        }, []) as Array<DurandalRouteConfiguration>;
}

function convertToDurandalRoute(leaf: leafMenuItem): DurandalRouteConfiguration {
    if (leaf.shardingMode) {
        return {
            route: leaf.route,
            title: leaf.title,
            moduleId: function () {
                const item = leaf.moduleId;
                const container = require('viewmodels/common/sharding/shardAwareContainer');
                return new container(leaf.shardingMode, item);
            },
            nav: leaf.nav,
            dynamicHash: leaf.dynamicHash,
            requiredAccess: leaf.requiredAccess
        };
    }
    return {
        route: leaf.route,
        title: leaf.title,
        moduleId: leaf.moduleId,
        nav: leaf.nav,
        dynamicHash: leaf.dynamicHash,
        requiredAccess: leaf.requiredAccess
    };
}

function getMenuItemDurandalRoutes(item: menuItem): Array<DurandalRouteConfiguration> {
    if (item.type === 'intermediate') {
        const intermediateItem = item as intermediateMenuItem;
        return intermediateItem.children
            .map(child => getMenuItemDurandalRoutes(child))
            .reduce((result, next) => result.concat(next), []);
    } else if (item.type === 'leaf') {
        return [convertToDurandalRoute(item as leafMenuItem)];
    }

    return [];
}

function generateAllMenuItems() {
    let appUrls = appUrl.forCurrentDatabase();
    return [
        getDocumentsMenuItem(appUrls),
        getIndexesMenuItem(appUrls),
        getSettingsMenuItem(appUrls),
        getTasksMenuItem(appUrls),
        getStatsMenuItem(appUrls),
        getDatabasesMenuItem(appUrls),
        getManageServerMenuItem(),
        rootItems.about(),
        rootItems.clusterDashboard(),
        rootItems.dashboard()
    ];
}



