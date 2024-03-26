import commandBase = require("commands/commandBase");
import endpoints = require("endpoints");
import database = require("models/resources/database");

class testAzureQueueStorageServerConnectionCommand extends commandBase {
    private readonly db: database;
    constructor(db: database, connectionString: string) {
        super();
        this.db = db;
    }

    execute(): JQueryPromise<Raven.Server.Web.System.NodeConnectionTestResult> {
        const url = endpoints.databases.queueEtlConnection.adminEtlQueueAzurequeuestorageTestConnection;

        return this.post<Raven.Server.Web.System.NodeConnectionTestResult>(url, JSON.stringify(payload), this.db, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError(`Failed to test Azure Queue Storage server connection`, response.responseText, response.statusText))
            .done((result: Raven.Server.Web.System.NodeConnectionTestResult) => {
                if (!result.Success) {
                    this.reportError(`Failed to test Azure Queue Storage server connection`, result.Error);
                }
            });
    }
}

export = testAzureQueueStorageServerConnectionCommand;
