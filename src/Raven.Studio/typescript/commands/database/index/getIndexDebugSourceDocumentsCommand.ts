import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getIndexDebugSourceDocumentsCommand extends commandBase {
    constructor(private db: database, private indexName: string, private startsWith: string, private skip = 0, private take = 256) {
        super();
    }

    execute(): JQueryPromise<arrayOfResultsAndCountDto<string>> {
        const args = {
            start: this.skip,
            pageSize: this.take,
            op: "source-doc-ids",
            name: this.indexName,
            startsWith: this.startsWith
        };

        const url = endpoints.databases.index.indexesDebug;
        return this.query(url, args, this.db);
    }
}

export = getIndexDebugSourceDocumentsCommand;
