import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getIndexesProgressCommand extends commandBase {

    constructor(private db: database, private location: databaseLocationSpecifier) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Indexes.IndexProgress[]> {
        const url = endpoints.databases.index.indexesProgress;
        const args = {
            ...this.location
        }
        const extractor = (response: resultsDto<Raven.Client.Documents.Indexes.IndexProgress>) => response.Results;
        return this.query(url, args, this.db, extractor)
            .fail((response: JQueryXHR) =>
                this.reportError("Failed to compute indexing progress!", response.responseText, response.statusText));
    }
} 

export = getIndexesProgressCommand;
