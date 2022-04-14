import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class toggleDatabaseCommand extends commandBase {

    constructor(private dbs: Array<database>, private disable: boolean) {
        super();
    }

    get action() {
        return this.disable ? "disable" : "enable";
    }

    execute(): JQueryPromise<statusDto<disableDatabaseResult>> {
        const payload: Raven.Client.ServerWide.Operations.ToggleDatabasesStateOperation.Parameters = {
            DatabaseNames: this.dbs.map(x => x.name)
        };

        const url = this.disable ?
            endpoints.global.adminDatabases.adminDatabasesDisable :
            endpoints.global.adminDatabases.adminDatabasesEnable;

        return this.post(url, JSON.stringify(payload))
            .fail((response: JQueryXHR) => this.reportError("Failed to toggle database status", response.responseText, response.statusText));
    }

}

export = toggleDatabaseCommand;  
