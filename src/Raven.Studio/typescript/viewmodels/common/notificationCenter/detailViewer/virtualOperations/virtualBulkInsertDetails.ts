import app from "durandal/app";

import abstractNotification from "common/notifications/models/abstractNotification";
import actionColumn from "widgets/virtualGrid/columns/actionColumn";
import notificationCenter from "common/notifications/notificationCenter";
import virtualBulkInsert from "common/notifications/models/virtualBulkInsert";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import virtualGridController from "widgets/virtualGrid/virtualGridController";
import textColumn from "widgets/virtualGrid/columns/textColumn";
import columnPreviewPlugin from "widgets/virtualGrid/columnPreviewPlugin";
import generalUtils from "common/generalUtils";
import moment from "moment";

class virtualBulkInsertDetails extends dialogViewModelBase {

    view = require("views/common/notificationCenter/detailViewer/virtualOperations/virtualBulkInsertDetails.html");

    private bulkInserts: virtualBulkInsert;
    private gridController = ko.observable<virtualGridController<virtualBulkOperationItem>>();
    private columnPreview = new columnPreviewPlugin<virtualBulkOperationItem>();
    
    constructor(bulkInserts: virtualBulkInsert) {
        super();
        
        this.bulkInserts = bulkInserts;
    }

    compositionComplete() {
        super.compositionComplete();

        const grid = this.gridController();
        grid.headerVisible(true);

        grid.init((s, t) => this.fetcher(s, t), () => {
            return [
                new textColumn<virtualBulkOperationItem>(grid, x => generalUtils.formatUtcDateAsLocal(x.date), "Date", "25%", {
                    sortable: x => x.date
                }),
                new textColumn<virtualBulkOperationItem>(grid, x => x.duration, "Duration (ms)", "15%", {
                    sortable: "number",
                    defaultSortOrder: "desc"
                }),
                new textColumn<virtualBulkOperationItem>(grid, x => x.documentsProcessed, "Documents", "15%", {
                    sortable: "number"
                }),
                new textColumn<virtualBulkOperationItem>(grid, x => x.attachmentsProcessed, "Attachments", "15%", {
                    sortable: "number"
                }),
                new textColumn<virtualBulkOperationItem>(grid, x => x.countersProcessed, "Counters", "15%", {
                    sortable: "number"
                }),
                new textColumn<virtualBulkOperationItem>(grid, x => x.timeSeriesProcessed, "Time Series", "15%", {
                    sortable: "number"
                })
            ];
        });

        this.columnPreview.install(".virtualBulkInsertDetails", ".js-virtual-bulk-insert-details-tooltip",
            (details: virtualBulkOperationItem,
             column: textColumn<virtualBulkOperationItem>,
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

    private fetcher(skip: number, take: number): JQueryPromise<pagedResult<virtualBulkOperationItem>> {
        return $.Deferred<pagedResult<virtualBulkOperationItem>>()
            .resolve({
                items: this.bulkInserts.operations(),
                totalResultCount: this.bulkInserts.operations().length
            });
    }

    static supportsDetailsFor(notification: abstractNotification) {
        return notification.type === "CumulativeBulkInsert";
    }

    static showDetailsFor(bulkInserts: virtualBulkInsert, center: notificationCenter) {
        return app.showBootstrapDialog(new virtualBulkInsertDetails(bulkInserts));
    }

}

export = virtualBulkInsertDetails;
