import commandBase from "commands/commandBase";
import endpoints from "endpoints";
import database from "models/resources/database";

class stopRecordingTransactionsCommand extends commandBase {

    constructor(private db: database) {
        super();
    }
    
    execute(): JQueryPromise<operationIdDto> {
        const url = endpoints.databases.transactionsRecording.adminTransactionsStopRecording;
        
        return this.post<void>(url, null, this.db, { dataType: undefined })
            .done(() => this.reportSuccess("Transaction Commands Recoding was stopped"))
            .fail((response: JQueryXHR) => this.reportError("Failed to stop recording transaction commands", response.responseText, response.statusText));
    }

}

export = stopRecordingTransactionsCommand;
