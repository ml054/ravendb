import app = require("durandal/app");
import abstractNotification = require("common/notifications/models/abstractNotification");
import notificationCenter = require("common/notifications/notificationCenter");
import alert = require("common/notifications/models/alert");
import abstractAlertDetails = require("viewmodels/common/notificationCenter/detailViewer/alerts/abstractAlertDetails");

class genericAlertDetails extends abstractAlertDetails {

    constructor(alert: alert, notificationCenter: notificationCenter) {
        super(alert, notificationCenter);
    }

    message = ko.pureComputed(() => {
        const details = this.alert.details() as Raven.Server.NotificationCenter.Notifications.Details.MessageDetails;
        if ("Message" in details) {
            return details.Message;
        }
        return null;
    });

    exception = ko.pureComputed(() => {
        const details = this.alert.details() as Raven.Server.NotificationCenter.Notifications.Details.ExceptionDetails;
        if ("Exception" in details) {
            return details.Exception;
        }
        return null;
    });

    static supportsDetailsFor(notification: abstractNotification) {
        /*
        Please notice we have custom providers for some of the alerts (ie. newVersionAvailable)
        but I don't exclude this here. Instead this provider is plugged as last one - so it is fallback */
        return notification instanceof alert;
    }

    static showDetailsFor(alert: alert, center: notificationCenter) {
        return app.showBootstrapDialog(new genericAlertDetails(alert, center));
    }
}

export = genericAlertDetails;
