import database from "models/resources/database";
import commandBase from "commands/commandBase";
import endpoint from "endpoints";

class saveRefreshConfigurationCommand extends commandBase {
    constructor(private db: database, private refreshConfiguration: Raven.Client.Documents.Operations.Refresh.RefreshConfiguration) {
        super();
    }

    execute(): JQueryPromise<updateDatabaseConfigurationsResult> {
        const url = endpoint.databases.refresh.adminRefreshConfig;
        const args = ko.toJSON(this.refreshConfiguration);
        return this.post<updateDatabaseConfigurationsResult>(url, args, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to save refresh configuration", response.responseText, response.statusText));
    }
}

export = saveRefreshConfigurationCommand;
