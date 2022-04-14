import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";
import { shardingTodo } from "common/developmentHelper";

class getIndexesStatsCommand extends commandBase {

    constructor(private db: database, private location?: databaseLocationSpecifier) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Indexes.IndexStats[]> {
        const url = endpoints.databases.index.indexesStats;
        const args = this.location;
        const extractor = (response: resultsDto<Raven.Client.Documents.Indexes.IndexStats>) => response.Results;
        return this.query(url, args, this.db, extractor)
            .fail((response: JQueryXHR) => this.reportError("Failed to load index statistics", response.responseText, response.statusText));
    }
}

export = getIndexesStatsCommand;
