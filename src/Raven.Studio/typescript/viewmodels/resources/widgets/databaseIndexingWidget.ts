import textColumn from "widgets/virtualGrid/columns/textColumn";
import indexingSpeedItem from "models/resources/widgets/indexingSpeedItem";
import clusterDashboard from "viewmodels/resources/clusterDashboard";
import nodeTagColumn from "widgets/virtualGrid/columns/nodeTagColumn";
import abstractDatabaseAndNodeAwareTableWidget from "viewmodels/resources/widgets/abstractDatabaseAndNodeAwareTableWidget";
import virtualColumn from "widgets/virtualGrid/columns/virtualColumn";
import appUrl from "common/appUrl";
import perNodeStatItems from "models/resources/widgets/perNodeStatItems";
import widget from "viewmodels/resources/widgets/widget";

class databaseIndexingWidget extends abstractDatabaseAndNodeAwareTableWidget<Raven.Server.Dashboard.Cluster.Notifications.DatabaseIndexingSpeedPayload, perNodeStatItems<indexingSpeedItem>, indexingSpeedItem> {

    view = require("views/resources/widgets/databaseIndexingWidget.html");
    
    getType(): Raven.Server.Dashboard.Cluster.ClusterDashboardNotificationType {
        return "DatabaseIndexing";
    }
    
    constructor(controller: clusterDashboard) {
        super(controller);

        for (const node of this.controller.nodes()) {
            const stats = new perNodeStatItems<indexingSpeedItem>(node.tag());
            this.nodeStats.push(stats);
        }
    }

    protected createNoDataItem(nodeTag: string, databaseName: string): indexingSpeedItem {
        return indexingSpeedItem.noData(nodeTag, databaseName);
    }

    protected mapItems(nodeTag: string, data: Raven.Server.Dashboard.Cluster.Notifications.DatabaseIndexingSpeedPayload): indexingSpeedItem[] {
        return data.Items.map(x => new indexingSpeedItem(nodeTag, x));
    }

    protected prepareColumns(containerWidth: number, results: pagedResult<indexingSpeedItem>): virtualColumn[] {
        const grid = this.gridController();
        return [
            new textColumn<indexingSpeedItem>(grid, x => x.hideDatabaseName ? "" : x.database, "Database", "35%"),
            new nodeTagColumn<indexingSpeedItem>(grid, item => this.prepareUrl(item, "Indexing Performance View")),
            new textColumn<indexingSpeedItem>(grid, x => widget.formatNumber(x.indexedPerSecond), "Indexed/s", "15%"),
            new textColumn<indexingSpeedItem>(grid, x => widget.formatNumber(x.mappedPerSecond), "Mapped/s", "15%"),
            new textColumn<indexingSpeedItem>(grid, x => widget.formatNumber(x.reducedPerSecond), "Reduced/s", "15%")
        ];
    }

    protected generateLocalLink(database: string): string {
        return appUrl.forIndexPerformance(database);
    }
}


export = databaseIndexingWidget;
