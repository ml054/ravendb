import commandBase from "commands/commandBase";
import database from "models/resources/database";
import document from "models/database/documents/document";
import endpoints from "endpoints";

class getRevisionsBinDocumentMetadataCommand extends commandBase {

    constructor(private id: string, private db: database) {
        super();

        if (!id) {
            throw new Error("Must specify ID");
        }
    }

    execute(): JQueryPromise<document> {
        const args = {
            id: this.id, 
            start: 0,
            pageSize: 1,
            metadataOnly: true
        }

        const url = endpoints.databases.revisions.revisions + this.urlEncodeArgs(args);

        return this.query(url, null, this.db, (results: resultsWithTotalCountDto<documentDto>) => {
            if (results.Results.length) {
                return new document(results.Results[0]);
            } else {
                return null;
            }
        });
    }
 }

export = getRevisionsBinDocumentMetadataCommand;
