import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class dismissNotificationCommand extends commandBase {

    constructor(private db: database, private notificationId: string, private forever: boolean = false) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            id: this.notificationId,
            forever: this.forever ? "true" : undefined
        };
        const url = this.db ? endpoints.databases.databaseNotificationCenter.notificationCenterDismiss : endpoints.global.serverNotificationCenter.serverNotificationCenterDismiss;

        return this.post(url + this.urlEncodeArgs(args), null, this.db, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError("Failed to dismiss action", response.responseText, response.statusText));
        
    }
}

export = dismissNotificationCommand;
