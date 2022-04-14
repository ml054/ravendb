import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class saveReplicationHubAccessConfigCommand extends commandBase {

    constructor(private db: database, private hubTaskName: string, 
                private replicationAccess: Raven.Client.Documents.Operations.Replication.ReplicationHubAccess) {
        super();
    }
    
    execute(): JQueryPromise<void> {

        const args = {
            name : this.hubTaskName
        };

        const url = endpoints.databases.pullReplication.adminTasksPullReplicationHubAccess + this.urlEncodeArgs(args);
        
        return this.put<void>(url, JSON.stringify(this.replicationAccess), this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to save the Hub Access Configuration", response.responseText, response.statusText))
            .done(() => this.reportSuccess(`Hub Access Configuration was saved successfully`));
    }
}

export = saveReplicationHubAccessConfigCommand;
