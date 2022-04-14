import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class backupNowPeriodicCommand extends commandBase {
    constructor(private db: database, private taskId: number, private isFullBackup: boolean, private taskName: string) {
        super();
    }
 
    execute(): JQueryPromise<Raven.Client.Documents.Operations.Backups.StartBackupOperationResult> {
        const url = endpoints.databases.ongoingTasks.adminBackupDatabase +
            this.urlEncodeArgs({
                taskId: this.taskId,
                isFullBackup: this.isFullBackup
            });

        return this.post(url, null, this.db)
            .fail(response => {
                this.reportError(`Failed to start a backup for task: '${this.taskName}'`,
                    response.responseText,
                    response.statusText);
            });
    }
}

export = backupNowPeriodicCommand; 

