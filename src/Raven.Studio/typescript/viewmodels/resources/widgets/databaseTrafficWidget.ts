import clusterDashboard from "viewmodels/resources/clusterDashboard";
import nodeTagColumn from "widgets/virtualGrid/columns/nodeTagColumn";
import abstractDatabaseAndNodeAwareTableWidget from "viewmodels/resources/widgets/abstractDatabaseAndNodeAwareTableWidget";
import virtualColumn from "widgets/virtualGrid/columns/virtualColumn";
import textColumn from "widgets/virtualGrid/columns/textColumn";
import appUrl from "common/appUrl";
import trafficWatchItem from "models/resources/widgets/trafficWatchItem";
import generalUtils from "common/generalUtils";
import perNodeStatItems from "models/resources/widgets/perNodeStatItems";
import widget from "viewmodels/resources/widgets/widget";

class databaseTrafficWidget extends abstractDatabaseAndNodeAwareTableWidget<Raven.Server.Dashboard.Cluster.Notifications.DatabaseTrafficWatchPayload, 
    perNodeStatItems<trafficWatchItem>, trafficWatchItem> {

    view = require("views/resources/widgets/databaseTrafficWidget.html");
    
    getType(): Raven.Server.Dashboard.Cluster.ClusterDashboardNotificationType {
        return "DatabaseTraffic";
    }

    constructor(controller: clusterDashboard) {
        super(controller);

        for (const node of this.controller.nodes()) {
            const stats = new perNodeStatItems<trafficWatchItem>(node.tag());
            this.nodeStats.push(stats);
        }
    }

    protected createNoDataItem(nodeTag: string, databaseName: string): trafficWatchItem {
        return trafficWatchItem.noData(nodeTag, databaseName);
    }

    protected mapItems(nodeTag: string, data: Raven.Server.Dashboard.Cluster.Notifications.DatabaseTrafficWatchPayload): trafficWatchItem[] {
        return data.Items.map(x => new trafficWatchItem(nodeTag, x));
    }

    protected prepareColumns(containerWidth: number, results: pagedResult<trafficWatchItem>): virtualColumn[] {
        const grid = this.gridController();
        return [
            new textColumn<trafficWatchItem>(grid, x => x.hideDatabaseName ? "" : x.database, "Database", "35%"),
            new nodeTagColumn<trafficWatchItem>(grid, item => this.prepareUrl(item, "Traffic Watch View")),
            new textColumn<trafficWatchItem>(grid, x => widget.formatNumber(x.requestsPerSecond), "Requests/s", "15%"),
            new textColumn<trafficWatchItem>(grid, x => widget.formatNumber(x.writesPerSecond), "Writes/s", "15%"),
            new textColumn<trafficWatchItem>(grid, x => x.noData ? "-" : generalUtils.formatBytesToSize(x.dataWritesPerSecond), "Data written/s", "15%"),
        ];
    }

    protected generateLocalLink(database: string): string {
        return appUrl.forTrafficWatch(database);
    }
}

export = databaseTrafficWidget;
