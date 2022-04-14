import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class executeBulkDocsCommand extends commandBase {
    constructor(public docs: Partial<Raven.Server.Documents.Handlers.BatchRequestParser.CommandData>[], private db: database, private transactionMode: Raven.Client.Documents.Session.TransactionMode = "SingleNode") {
        super();
    }

    execute(): JQueryPromise<resultsDto<Raven.Server.Documents.Handlers.BatchRequestParser.CommandData>> {
        return this.post<Raven.Server.Documents.Handlers.BatchRequestParser.CommandData[]>(endpoints.databases.batch.bulk_docs, JSON.stringify({ Commands: this.docs, TransactionMode: this.transactionMode }), this.db);
    }
}

export = executeBulkDocsCommand; 
