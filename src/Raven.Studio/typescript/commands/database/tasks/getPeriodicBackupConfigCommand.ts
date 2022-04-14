import commandBase from "commands/commandBase";
import endpoints from "endpoints";
import database from "models/resources/database";

class getPeriodicBackupConfigCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<periodicBackupServerLimitsResponse> {
        const url = endpoints.databases.ongoingTasks.adminPeriodicBackupConfig;

        return this.query<periodicBackupServerLimitsResponse>(url, null, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to get periodic backup configuration", response.responseText, response.statusText));
    }
}

export = getPeriodicBackupConfigCommand;
