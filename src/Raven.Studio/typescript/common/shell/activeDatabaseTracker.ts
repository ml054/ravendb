
import EVENTS from "common/constants/events";
import database from "models/resources/database";
import databaseDisconnectedEventArgs from "viewmodels/resources/databaseDisconnectedEventArgs";
import router from "plugins/router";
import messagePublisher from "common/messagePublisher";
import databaseSettings from "common/settings/databaseSettings";
import studioSettings from "common/settings/studioSettings";

class activeDatabaseTracker {

    static default: activeDatabaseTracker = new activeDatabaseTracker();

    database: KnockoutObservable<database> = ko.observable<database>();
    
    settings: KnockoutObservable<databaseSettings> = ko.observable<databaseSettings>();

    constructor() {
;

        studioSettings.default.init(this.settings);
    }

    onActivation(db: database): JQueryPromise<void> {
        const task = $.Deferred<void>();

        // If the 'same' database was selected from the top databases selector dropdown, 
        // then we want the knockout observable to be aware of it so that scrolling on page will occur
        if (db === this.database()) {
            this.database(null);
        }

        studioSettings.default.forDatabase(db)
            .done((settings) => {

                this.settings(settings);
                // Set the active database
                this.database(db);
                task.resolve();
            })
            .fail(() => task.reject());

        return task;
    }

    private onDatabasesPage() {
        const instruction = router.activeInstruction();
        if (!instruction) {
            return false;
        }
        return instruction.fragment === "databases";
    }

}

export = activeDatabaseTracker;
