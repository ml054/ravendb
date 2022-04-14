import commandBase from "commands/commandBase";
import database from "models/resources/database";
import document from "models/database/documents/document";
import endpoints from "endpoints";
import documentMetadata from "models/database/documents/documentMetadata";

class getDocumentMetadataCommand extends commandBase {

    constructor(private id: string, private db: database, private shouldResolveNotFoundAsNull: boolean = false) {
        super();

        if (!id) {
            throw new Error("Must specify ID");
        }
    }

    execute(): JQueryPromise<documentMetadata> {
        const documentResult = $.Deferred<any>();
        const payload = {
            Ids: [this.id]
        }
        const postResult = this.post<queryResultDto<documentMetadataDto>>(endpoints.databases.document.docs, JSON.stringify(payload), this.db);
        postResult.fail((xhr: JQueryXHR) => {
            if (this.shouldResolveNotFoundAsNull && xhr.status === 404) {
                documentResult.resolve(null);
            } else {
                documentResult.reject(xhr);
            }
        });
        postResult.done((queryResult: queryResultDto<documentMetadataDto>) => {
            if (queryResult.Results.length === 0) {
                if (this.shouldResolveNotFoundAsNull) {
                    documentResult.resolve(null);
                } else {
                    documentResult.reject("Unable to find document with ID " + this.id);
                }
            } else {
                const doc = new document(queryResult.Results[0]);
                documentResult.resolve(doc.__metadata);
            }
        });

        return documentResult;
    }
 }

 export = getDocumentMetadataCommand;
