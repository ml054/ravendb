import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getPeriodicBackupConfigurationCommand extends commandBase {
    constructor(private db: database, private taskId: number) {
        super();
    }
 
    execute(): JQueryPromise<Raven.Client.Documents.Operations.Backups.PeriodicBackupConfiguration> {
        const url = endpoints.global.backupDatabase.periodicBackup +
            this.urlEncodeArgs({ name: this.db.name, taskId: this.taskId });

        return this.query<Raven.Client.Documents.Operations.Backups.PeriodicBackupConfiguration>(url, null)
            .fail((response: JQueryXHR) => {
                this.reportError(`Failed to get periodic backup configuration for task: ${this.taskId}`,
                    response.responseText, response.statusText);
            });
    }
}

export = getPeriodicBackupConfigurationCommand; 

