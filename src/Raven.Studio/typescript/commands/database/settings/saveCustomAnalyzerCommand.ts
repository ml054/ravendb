import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class saveCustomAnalyzerCommand extends commandBase {

    constructor(private db: database, private analyzerDto: Raven.Client.Documents.Indexes.Analysis.AnalyzerDefinition) {
        super();
    }

    execute(): JQueryPromise<void> {
        const url = endpoints.databases.adminAnalyzers.adminAnalyzers;
        const payload = {
            Analyzers: [ this.analyzerDto ]
        };

        return this.put<void>(url, JSON.stringify(payload), this.db, { dataType: undefined })
            .fail((response: JQueryXHR) => {
                this.reportError("Failed to save custom analyzer", response.responseText, response.statusText);
            })
            .done(() => {
                this.reportSuccess(`Saved custom analyzer ${this.analyzerDto.Name}`);
            });
    }
}

export = saveCustomAnalyzerCommand; 

