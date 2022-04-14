import database from "models/resources/database";
import commandBase from "commands/commandBase";
import endpoint from "endpoints";

class saveRevisionsForConflictsConfigurationCommand extends commandBase {
    constructor(private db: database, private revisionsConfiguration: Raven.Client.Documents.Operations.Revisions.RevisionsCollectionConfiguration) {
        super();
    }

    execute(): JQueryPromise<updateDatabaseConfigurationsResult> {

        const url = endpoint.databases.revisions.adminRevisionsConflictsConfig;
        const args = ko.toJSON(this.revisionsConfiguration);
        return this.post<updateDatabaseConfigurationsResult>(url, args, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to save revisions for conflicts configuration", response.responseText, response.statusText));

    }
}

export = saveRevisionsForConflictsConfigurationCommand;
