import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getDocumentRevisionsCountCommand extends commandBase {

    constructor(private id: string, private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Session.Operations.GetRevisionsCountOperation.DocumentRevisionsCount> {
        const args = {
            id: this.id
        };

        const url = endpoints.databases.revisions.revisionsCount + this.urlEncodeArgs(args);

        return this.query<Raven.Client.Documents.Session.Operations.GetRevisionsCountOperation.DocumentRevisionsCount>(url, null, this.db)
            .fail((result: JQueryXHR) => this.reportError("Failed to get revisions count", result.responseText, result.statusText));
    }
 }

export = getDocumentRevisionsCountCommand;
