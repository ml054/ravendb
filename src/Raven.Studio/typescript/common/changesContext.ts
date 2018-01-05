/// <reference path="../../typings/tsd.d.ts" />

import changesApi = require("common/changesApi");
import database = require("models/resources/database");
import changeSubscription = require("common/changeSubscription");
import appUrl = require("common/appUrl");
import router = require("plugins/router");
import serverNotificationCenterClient = require("common/serverNotificationCenterClient");
import databaseNotificationCenterClient = require("common/databaseNotificationCenterClient");
import EVENTS = require("common/constants/events");

import databaseDisconnectedEventArgs = require("viewmodels/resources/databaseDisconnectedEventArgs");
import notificationCenter = require("common/notifications/notificationCenter");
import collectionsTracker = require("common/helpers/database/collectionsTracker");

class changesContext {
    static default = new changesContext();
    
    serverNotifications = ko.observable<serverNotificationCenterClient>();
    leaderNotifications = ko.observable<serverNotificationCenterClient>();
    databaseNotifications = ko.observable<databaseNotificationCenterClient>();

    databaseChangesApi = ko.observable<changesApi>();
    private pendingAfterChangesApiConnectedHandlers = [] as Function[];
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

    // Notifications from the Server for which the studio is open
    connectServerWideNotificationCenter(): JQueryPromise<void> {
        const alreadyHasGlobalChangesApi = this.serverNotifications();
        
        if (alreadyHasGlobalChangesApi) {
            return alreadyHasGlobalChangesApi.connectToWebSocketTask;
        }

        const serverClient = new serverNotificationCenterClient();
        this.serverNotifications(serverClient);

        return serverClient.connectToWebSocketTask;
    }

    // Notifications from the Leader - regardlsess of which server the studio is open on
    connectLeaderNotificationCenter(leaderUrl: string): JQueryPromise<void> {
        const alreadyHasGlobalChangesApi = this.leaderNotifications();
        
        if (alreadyHasGlobalChangesApi){
            // Close because we need to connect to a 'new' leader
            alreadyHasGlobalChangesApi.dispose(); 
        }
        
        const leaderClient = new serverNotificationCenterClient(leaderUrl, true);
        this.leaderNotifications(leaderClient);

        return leaderClient.connectToWebSocketTask;
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

        if (db.disabled() || !db.relevant()) { //TODO: or not licensed
            this.navigateToResourceSpecificPage(db);
            return;
        }
       
        const notificationsClient = new databaseNotificationCenterClient(db, null);

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

            ko.postbox.publish(EVENTS.Database.Disconnect, { database: currentChanges.getDatabase(), cause: cause } as databaseDisconnectedEventArgs);
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
