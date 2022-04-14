import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class deleteIndexCommand extends commandBase {
    constructor(private indexName: string, private db: database) {
        super();
    }

    execute(): JQueryPromise<any> {
        const args = {
            name: this.indexName
        };

        const url = endpoints.databases.index.indexes + this.urlEncodeArgs(args);

        return this.del(url, null, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to delete index " + this.indexName, response.responseText))
            .done(() => this.reportSuccess("Deleted " + this.indexName));
    }
}

export = deleteIndexCommand;
