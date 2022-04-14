import app from "durandal/app";

import operation from "common/notifications/models/operation";
import abstractNotification from "common/notifications/models/abstractNotification";
import notificationCenter from "common/notifications/notificationCenter";
import abstractOperationDetails from "viewmodels/common/notificationCenter/detailViewer/operations/abstractOperationDetails";

class dumpRawIndexDataDetails extends abstractOperationDetails {
    
    view = require("views/common/notificationCenter/detailViewer/operations/dumpRawIndexDataDetails.html");
    
    progress: KnockoutObservable<Raven.Server.Documents.Handlers.Admin.AdminIndexHandler.DumpIndexResult>;

    constructor(op: operation, notificationCenter: notificationCenter) {
        super(op, notificationCenter);
        
        this.initObservables();
    }

    initObservables() {
        super.initObservables();
        
        this.progress = ko.pureComputed(() => {
            return (this.op.progress() || this.op.result()) as Raven.Server.Documents.Handlers.Admin.AdminIndexHandler.DumpIndexResult
        });
    }

    static supportsDetailsFor(notification: abstractNotification) {
        return (notification instanceof operation) && notification.taskType() === "DumpRawIndexData";
    }

    static showDetailsFor(op: operation, center: notificationCenter) {
        return app.showBootstrapDialog(new dumpRawIndexDataDetails(op, center));
    }
}

export = dumpRawIndexDataDetails;
