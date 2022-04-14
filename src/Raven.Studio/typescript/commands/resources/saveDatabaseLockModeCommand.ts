import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";
import { DatabaseSharedInfo } from "../../components/models/databases";

class saveDatabaseLockModeCommand extends commandBase {

    constructor(private dbs: Array<database | DatabaseSharedInfo>, private lockMode: Raven.Client.ServerWide.DatabaseLockMode) {
        super();
    }

    execute(): JQueryPromise<void> {
        const payload: Raven.Client.ServerWide.Operations.SetDatabasesLockOperation.Parameters = {
            DatabaseNames: this.dbs.map(x => x.name),
            Mode: this.lockMode
        };

        const url = endpoints.global.adminDatabases.adminDatabasesSetLock;

        return this.post(url, JSON.stringify(payload), null, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError("Failed to toggle database lock mode", response.responseText, response.statusText));
    }

}

export = saveDatabaseLockModeCommand;  
