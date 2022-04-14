import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getIndexHistoryCommand extends commandBase {

    constructor(private db: database, private indexName: string) {
        super();
    }

    execute(): JQueryPromise<indexHistoryCommandResult> {
        const args = { name: this.indexName };
        const url = endpoints.databases.index.indexesHistory;
        
        return this.query<indexHistoryCommandResult>(url, args, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to get index history", response.responseText, response.statusText));
    }
} 

export = getIndexHistoryCommand;
