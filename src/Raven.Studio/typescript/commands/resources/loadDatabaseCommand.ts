import commandBase from "commands/commandBase";
import endpoints from "endpoints";
import database from "models/resources/database";

class loadDatabaseCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<void> {
        const url = endpoints.databases.stats.stats;
        return this.query<void>(url, null, this.db, null, null, this.getTimeToAlert(true));
    }
}

export = loadDatabaseCommand;
