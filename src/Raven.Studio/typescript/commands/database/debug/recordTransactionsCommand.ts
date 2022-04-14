import commandBase from "commands/commandBase";
import endpoints from "endpoints";
import database from "models/resources/database";

class recordTransactionsCommand extends commandBase {

    constructor(private db: database, private targetPath: string) {
        super();
    }
    
    execute(): JQueryPromise<operationIdDto> {
        const url = endpoints.databases.transactionsRecording.adminTransactionsStartRecording;
        const payload: Raven.Client.Documents.Operations.TransactionsRecording.StartTransactionsRecordingOperation.Parameters = {
            File: this.targetPath
        };
        
        return this.post<operationIdDto>(url, JSON.stringify(payload), this.db, { dataType: undefined })
            .done(() => this.reportSuccess("Transaction Commands Recoding was started for database: " + this.db.name))
            .fail((response: JQueryXHR) => this.reportError("Failed to start recording transaction commands", response.responseText, response.statusText));
    }

}

export = recordTransactionsCommand;
