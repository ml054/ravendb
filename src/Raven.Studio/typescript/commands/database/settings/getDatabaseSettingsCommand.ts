import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getDatabaseSettingsCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Server.Config.SettingsResult> {
        const url = endpoints.databases.adminConfiguration.adminConfigurationSettings;

        return this.query<Raven.Server.Config.SettingsResult>(url, null, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to load the Database Settings", response.responseText, response.statusText));
    }
}

export = getDatabaseSettingsCommand;
