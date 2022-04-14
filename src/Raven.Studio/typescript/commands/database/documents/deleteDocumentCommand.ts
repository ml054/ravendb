import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class deleteDocumentCommand extends commandBase {

    constructor(private docId: string, private db: database) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            id: this.docId
        };
        const url = endpoints.databases.document.docs + this.urlEncodeArgs(args);
        return this.del<void>(url, null, this.db);
    }
}

export = deleteDocumentCommand;
