import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class adminJsScriptCommand extends commandBase {
    constructor(private script: string, private targetDatabase?: string) {
        super();
    }

    execute(): JQueryPromise<{Result: any}> {
        const args = {
            serverScript: !this.targetDatabase,
            database: this.targetDatabase
        };

        const payload: Raven.Server.Documents.Patch.AdminJsScript = {
            Script: this.script
        };

        const url = endpoints.global.adminDatabases.adminConsole + this.urlEncodeArgs(args);
        return this.post(url, JSON.stringify(payload))
            .done(() => this.reportSuccess("Script executed"))
            .fail((response: JQueryXHR) => this.reportError("Script failed", response.responseText, response.statusText));
    }
}

export = adminJsScriptCommand;
