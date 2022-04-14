import database from "models/resources/database";
import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class saveConflictSolverConfigurationCommand extends commandBase {
    constructor(private db: database, private configuration: Raven.Client.ServerWide.ConflictSolver) {
        super(); 
    }

    execute(): JQueryPromise<updateDatabaseConfigurationsResult> {
        const urlArgs = {
            name: this.db.name
        };
        const url = endpoints.global.adminDatabases.adminReplicationConflictsSolver + this.urlEncodeArgs(urlArgs);
        const args = ko.toJSON(this.configuration);
        return this.post<updateDatabaseConfigurationsResult>(url, args)
            .done(() => this.reportSuccess("Conflict solver configuration was saved"))
            .fail((response: JQueryXHR) => this.reportError("Failed to save conflict solver configuration", response.responseText, response.statusText));

    }
}

export = saveConflictSolverConfigurationCommand;
