import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class deleteCounterCommand extends commandBase {

    constructor(private counterName: string, private documentId: string, private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Operations.Counters.CounterBatch> {
        const payload: Omit<Raven.Client.Documents.Operations.Counters.CounterBatch, "FromEtl"> = {
            ReplyWithAllNodesValues: true,
            Documents:
                [{
                    DocumentId: this.documentId,
                    Operations:
                        [{
                            Type: "Delete",
                            Delta: undefined,
                            CounterName: this.counterName
                        }]
                }]
        }; 

        const url = endpoints.databases.counters.counters;

        return this.post<Raven.Client.Documents.Operations.Counters.CountersDetail>(url, JSON.stringify(payload), this.db)
            .done(() => {
                this.reportSuccess("Counter deleted successfully: " + this.counterName);
            })
            .fail((response: JQueryXHR) => this.reportError("Failed to delete counter", response.responseText, response.statusText));
    }
}

export = deleteCounterCommand;
