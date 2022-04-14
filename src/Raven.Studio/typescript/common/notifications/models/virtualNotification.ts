import abstractNotification from "common/notifications/models/abstractNotification";
import database from "models/resources/database";

abstract class virtualNotification extends abstractNotification {

    protected constructor(db: database, dto: Raven.Server.NotificationCenter.Notifications.Notification) {
        super(db, dto);
        
        this.requiresRemoteDismiss(false);
    }
}

export = virtualNotification;
