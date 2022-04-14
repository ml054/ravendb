import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class saveDatabaseRecordCommand extends commandBase {
    constructor(private db: database, private databaseRecord: documentDto, private etag: number) {
        super();

        if (!db) {
            throw new Error("Must specify database");
        }
    }
    
    execute(): JQueryPromise<void> {
        const url = endpoints.global.adminDatabases.adminDatabases;
        
        return this.put<void>(url, JSON.stringify(this.databaseRecord), null, { headers: { "ETag": this.etag }})
            .done(() => this.reportSuccess("Database Record was saved successfully"))
            .fail((response: JQueryXHR) => this.reportError("Failed to save Database Record", response.responseText, response.statusText));
    }
}

export = saveDatabaseRecordCommand;
