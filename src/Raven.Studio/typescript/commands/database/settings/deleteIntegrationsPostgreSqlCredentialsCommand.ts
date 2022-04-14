import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";
import genUtils from "common/generalUtils";

class deleteIntegrationsPostgreSqlCredentialsCommand extends commandBase {

    constructor(private db: database, private username: string) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            username: this.username
        };
        
        const url = endpoints.databases.postgreSqlIntegration.adminIntegrationsPostgresqlUser + this.urlEncodeArgs(args);

        return this.del<void>(url, null, this.db)
            .done(() => this.reportSuccess(`Successfully deleted credentials for user: ${genUtils.escapeHtml(this.username)}`))
            .fail((response: JQueryXHR) => this.reportError(`Failed to delete credentials for user: ${genUtils.escapeHtml(this.username)}`, response.responseText, response.statusText));
    }
}

export = deleteIntegrationsPostgreSqlCredentialsCommand;
