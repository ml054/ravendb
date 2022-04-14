import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getIndexDefaultsCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Server.Web.Studio.Processors.IndexDefaults> {
        const url = endpoints.databases.studioDatabaseTasks.studioTasksIndexesConfigurationDefaults;
        return this.query<Raven.Server.Web.Studio.Processors.IndexDefaults>(url, null, this.db)
            .fail((response: JQueryXHR) => {
                this.reportError("Failed to get index defaults!", response.responseText, response.statusText);
            });
    }
}

export = getIndexDefaultsCommand;
