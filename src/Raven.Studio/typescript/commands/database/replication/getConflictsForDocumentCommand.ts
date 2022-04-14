import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getConflictsForDocumentCommand extends commandBase {

    constructor(private ownerDb: database, private documentId: string) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Commands.GetConflictsResult> {
        const args = {
            docId: this.documentId
        };
        const url = endpoints.databases.replication.replicationConflicts + this.urlEncodeArgs(args);

        return this.query<Raven.Client.Documents.Commands.GetConflictsResult>(url, null, this.ownerDb);
    }
}

export = getConflictsForDocumentCommand;
