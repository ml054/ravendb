import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getSubscriptionConnectionDetailsCommand extends commandBase {

    constructor(private db: database, private taskId: number, private taskName: string, private baseUrl: string) {
        super();
    }
    
    execute(): JQueryPromise<Raven.Server.Documents.TcpHandlers.SubscriptionConnectionsDetails> {
        return this.getConnectionDetails()
            .fail((response: JQueryXHR) => {
                this.reportError(`Failed to get connection details`, response.responseText, response.statusText);
            });
    }

    private getConnectionDetails(): JQueryPromise<Raven.Server.Documents.TcpHandlers.SubscriptionConnectionsDetails> {
        const url = endpoints.databases.subscriptions.subscriptionsConnectionDetails;
        const args = { id: this.taskId, name: this.taskName };

        // Note: The 'relative url' has to be prefixed with the 'base url' 
        //       because the connection info (held by the responsible node) might Not be in the server that is on the current browser location
        return this.query<any>(url, args, this.db, null, null, 9000, this.baseUrl);
    }
}

export = getSubscriptionConnectionDetailsCommand; 
