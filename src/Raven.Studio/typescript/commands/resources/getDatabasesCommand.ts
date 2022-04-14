import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getDatabasesCommand extends commandBase {

    execute(): JQueryPromise<Raven.Client.ServerWide.Operations.DatabasesInfo> {
        const url = endpoints.global.databases.databases;

        return this.query<Raven.Client.ServerWide.Operations.DatabasesInfo>(url, null)
            .fail((response: JQueryXHR) => this.reportError("Failed to load databases", response.responseText, response.statusText));
    }
}

export = getDatabasesCommand;
