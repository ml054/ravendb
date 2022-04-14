/// <reference path="../../typings/tsd.d.ts" />

import changesApi from "common/changesApi";
import database from "models/resources/database";
import changeSubscription from "common/changeSubscription";
import appUrl from "common/appUrl";
import router from "plugins/router";
import serverNotificationCenterClient from "common/serverNotificationCenterClient";
import databaseNotificationCenterClient from "common/databaseNotificationCenterClient";
import EVENTS from "common/constants/events";

import databaseDisconnectedEventArgs from "viewmodels/resources/databaseDisconnectedEventArgs";
import notificationCenter from "common/notifications/notificationCenter";
import collectionsTracker from "common/helpers/database/collectionsTracker";

class changesContext {
    static default = new changesContext();
    
    serverNotifications = ko.observable<serverNotificationCenterClient>();
    databaseNotifications = ko.observable<databaseNotificationCenterClient>();

    databaseChangesApi = ko.observable<changesApi>();
    private pendingAfterChangesApiConnectedHandlers: Function[] = [];
    private hasChangesApiConnected = false;

    private globalDatabaseSubscriptions: changeSubscription[] = [];

    constructor() {
        window.addEventListener("unload", () => {
            this.disconnectFromDatabaseChangesApi("ChangingDatabase");
            this.serverNotifications().dispose();

            if (this.databaseNotifications()) {
                this.databaseNotifications().dispose();
            }
        });

        this.databaseChangesApi.subscribe(newValue => {
            if (!newValue) {
                this.hasChangesApiConnected = false;
            } else {
                this.hasChangesApiConnected = true;

                this.pendingAfterChangesApiConnectedHandlers.forEach(handler => handler());
                this.pendingAfterChangesApiConnectedHandlers = [];
            }
        });
    }

    afterChangesApiConnected(func: Function) {
        if (this.hasChangesApiConnected) {
            func();
            return;
        }

        this.pendingAfterChangesApiConnectedHandlers.push(func);
    }

    connectServerWideNotificationCenter(): JQueryPromise<void> {
        const alreadyHasGlobalChangesApi = this.serverNotifications();
        if (alreadyHasGlobalChangesApi) {
            return alreadyHasGlobalChangesApi.connectToWebSocketTask;
        }

        const serverClient = new serverNotificationCenterClient();
        this.serverNotifications(serverClient);

        return serverClient.connectToWebSocketTask;
    }

    changeDatabase(db: database): void {
        const currentChanges = this.databaseChangesApi();
        if (currentChanges && currentChanges.getDatabase().name === db.name) {
            // nothing to do - already connected to requested changes api
            return;
        }

        if (currentChanges) {
            this.disconnect("ChangingDatabase");
        }

        if (db.disabled() || !db.relevant()) {
            this.navigateToResourceSpecificPage(db);
            return;
        }

        const notificationsClient = new databaseNotificationCenterClient(db);

        this.globalDatabaseSubscriptions.push(...notificationCenter.instance.configureForDatabase(notificationsClient));

        const newChanges = new changesApi(db);
        newChanges.connectToWebSocketTask.done(() => {
            this.databaseChangesApi(newChanges);
            this.navigateToResourceSpecificPage(db);
        });

        collectionsTracker.default.onDatabaseChanged(db);
        
        this.databaseNotifications(notificationsClient);
    }

    private navigateToResourceSpecificPage(db: database) {
        const locationHash = window.location.hash;
        const isMainPage = locationHash === appUrl.forDatabases();
        if (!isMainPage) {
            const updatedUrl = appUrl.forCurrentPage(db);
            if (updatedUrl) {
                router.navigate(updatedUrl);
            }
        }
    }

    private disconnectFromDatabaseNotificationCenter() {
        const currentClient = this.databaseNotifications();
        if (currentClient) {
            currentClient.dispose();
            this.databaseNotifications(null);
        }
    }

    private disconnectFromDatabaseChangesApi(cause: databaseDisconnectionCause) {
        const currentChanges = this.databaseChangesApi();
        if (currentChanges) {
            currentChanges.dispose();
            this.databaseChangesApi(null);

            const args: databaseDisconnectedEventArgs = {
                databaseName: currentChanges.getDatabase().name, 
                cause
            };
            ko.postbox.publish(EVENTS.Database.Disconnect, args);
        }
    }

    disconnectIfCurrent(db: database, cause: databaseDisconnectionCause) {
        const currentChanges = this.databaseChangesApi();

        if (currentChanges && currentChanges.getDatabase().name === db.name) {
            this.disconnect(cause);
        }
    }

    private disconnect(cause: databaseDisconnectionCause) {
        this.globalDatabaseSubscriptions.forEach(x => x.off());
        this.globalDatabaseSubscriptions = [];

        this.disconnectFromDatabaseChangesApi(cause);
        this.disconnectFromDatabaseNotificationCenter();
        notificationCenter.instance.databaseDisconnected();
    }
}

export = changesContext;
