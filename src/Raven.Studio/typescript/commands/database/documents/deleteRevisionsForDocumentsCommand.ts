import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class deleteRevisionsForDocumentsCommand extends commandBase {

    constructor(private ids: Array<string>, private db: database) {
        super();
    }

    execute(): JQueryPromise<void> {
        const payload: Raven.Server.Documents.Handlers.Admin.AdminRevisionsHandler.Parameters = {
            DocumentIds: this.ids
        };

        const url = endpoints.databases.adminRevisions.adminRevisions;

        return this.del<void>(url, JSON.stringify(payload), this.db, { dataType: undefined });
    }
 }

export = deleteRevisionsForDocumentsCommand;
