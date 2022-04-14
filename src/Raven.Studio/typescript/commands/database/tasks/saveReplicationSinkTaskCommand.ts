import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class saveReplicationSinkTaskCommand extends commandBase {

    constructor(private db: database, private replicationSettings: Raven.Client.Documents.Operations.Replication.PullReplicationAsSink) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Operations.Replication.PullReplicationAsSink> {
        const url = endpoints.databases.pullReplication.adminTasksSinkPullReplication;
        const payload = {
            PullReplicationAsSink: this.replicationSettings
        };

        return this.post<Raven.Client.Documents.Operations.Replication.PullReplicationAsSink>(url, JSON.stringify(payload), this.db)
            .fail((response: JQueryXHR) => {
                this.reportError("Failed to save the Replication Sink task", response.responseText, response.statusText);
            })
            .done(() => {
                this.reportSuccess(`Replication Sink task was saved successfully`);
            });
    }
}

export = saveReplicationSinkTaskCommand; 

