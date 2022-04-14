import app from "durandal/app";
import abstractNotification from "common/notifications/models/abstractNotification";
import notificationCenter from "common/notifications/notificationCenter";
import recentError from "common/notifications/models/recentError";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";

class recentErrorDetails extends dialogViewModelBase {

    view = require("views/common/notificationCenter/detailViewer/recentErrorDetails.html");

    protected readonly recentError: recentError;
    protected readonly dismissFunction: () => void;

    constructor(recentError: recentError, notificationCenter: notificationCenter) {
        super();
        this.bindToCurrentInstance("close", "dismiss");

        this.recentError = recentError;
        this.dismissFunction = () => notificationCenter.dismiss(recentError);
    }

    dismiss() {
        this.dismissFunction();
        this.close();
    }

    static supportsDetailsFor(notification: abstractNotification) {
        return notification instanceof recentError;
    }

    static showDetailsFor(recentError: recentError, center: notificationCenter) {
        return app.showBootstrapDialog(new recentErrorDetails(recentError, center));
    }
}

export = recentErrorDetails;
