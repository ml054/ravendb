import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getDatabaseLocationCommand extends commandBase {

    constructor(private inputName: string, private inputPath: string) {
        super();
    }

    execute(): JQueryPromise<Raven.Server.Web.Studio.DataDirectoryResult> {
        const args = {
            name: this.inputName,
            path: this.inputPath,
            getNodesInfo: true
        };

        const url = endpoints.global.studioTasks.adminStudioTasksFullDataDirectory + this.urlEncodeArgs(args);

        return this.query<Raven.Server.Web.Studio.DataDirectoryResult>(url, null, null)
            .fail((response: JQueryXHR) => this.reportError("Failed to calculate the database full path", response.responseText, response.statusText));
    }
}

export = getDatabaseLocationCommand;
