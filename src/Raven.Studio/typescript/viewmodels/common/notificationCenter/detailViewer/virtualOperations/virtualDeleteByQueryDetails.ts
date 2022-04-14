import app from "durandal/app";

import abstractNotification from "common/notifications/models/abstractNotification";
import actionColumn from "widgets/virtualGrid/columns/actionColumn";
import notificationCenter from "common/notifications/notificationCenter";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import virtualGridController from "widgets/virtualGrid/virtualGridController";
import textColumn from "widgets/virtualGrid/columns/textColumn";
import columnPreviewPlugin from "widgets/virtualGrid/columnPreviewPlugin";
import generalUtils from "common/generalUtils";
import virtualDeleteByQuery from "common/notifications/models/virtualDeleteByQuery";
import moment from "moment";

class virtualDeleteByQueryDetails extends dialogViewModelBase {

    view = require("views/common/notificationCenter/detailViewer/virtualOperations/virtualDeleteByQueryDetails.html");

    private virtualNotification: virtualDeleteByQuery;
    private gridController = ko.observable<virtualGridController<queryBasedVirtualBulkOperationItem>>();
    private columnPreview = new columnPreviewPlugin<queryBasedVirtualBulkOperationItem>();

    constructor(virtualNotification: virtualDeleteByQuery) {
        super();

        this.virtualNotification = virtualNotification;
    }

    compositionComplete() {
        super.compositionComplete();

        const grid = this.gridController();
        grid.headerVisible(true);

        grid.init((s, t) => this.fetcher(s, t), () => {
            return [
                new textColumn<queryBasedVirtualBulkOperationItem>(grid, x => generalUtils.formatUtcDateAsLocal(x.date), "Date", "25%", {
                    sortable: x => x.date
                }),
                new textColumn<queryBasedVirtualBulkOperationItem>(grid, x => x.duration, "Duration (ms)", "15%", {
                    sortable: "number",
                    defaultSortOrder: "desc"
                }),
                new textColumn<queryBasedVirtualBulkOperationItem>(grid, x => x.totalItemsProcessed, "Processed documents", "15%", {
                    sortable: "number"
                }),
                new textColumn<queryBasedVirtualBulkOperationItem>(grid, x => x.indexOrCollectionUsed, "Collection/Index", "20%", {
                    sortable: "string"
                }),
                new textColumn<queryBasedVirtualBulkOperationItem>(grid, x => x.query, "Query", "25%", {
                    sortable: "string"
                }),
            ];
        });

        this.columnPreview.install(".virtualDeleteByQueryDetails", ".js-virtual-delete-by-query-details-tooltip",
            (details: queryBasedVirtualBulkOperationItem,
             column: textColumn<queryBasedVirtualBulkOperationItem>,
             e: JQueryEventObject, onValue: (context: any, valueToCopy?: string) => void) => {
                if (!(column instanceof actionColumn)) {
                    if (column.header === "Date") {
                        onValue(moment.utc(details.date), details.date);
                    } else {
                        const value = column.getCellValue(details);
                        if (value) {
                            onValue(generalUtils.escapeHtml(value), value);
                        }
                    }
                }
            });
    }

    private fetcher(skip: number, take: number): JQueryPromise<pagedResult<queryBasedVirtualBulkOperationItem>> {
        return $.Deferred<pagedResult<queryBasedVirtualBulkOperationItem>>()
            .resolve({
                items: this.virtualNotification.operations(),
                totalResultCount: this.virtualNotification.operations().length
            });
    }

    static supportsDetailsFor(notification: abstractNotification) {
        return notification.type === "CumulativeDeleteByQuery";
    }

    static showDetailsFor(virtualNotification: virtualDeleteByQuery, center: notificationCenter) {
        return app.showBootstrapDialog(new virtualDeleteByQueryDetails(virtualNotification));
    }

}

export = virtualDeleteByQueryDetails;
