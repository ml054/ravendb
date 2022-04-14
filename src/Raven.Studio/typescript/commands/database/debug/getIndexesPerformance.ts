import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getIndexesPerformance extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Indexes.IndexPerformanceStats[]> {
        const url = endpoints.databases.index.indexesPerformance;
        return this.query<Raven.Client.Documents.Indexes.IndexPerformanceStats[]>(url, null, this.db);
    }
}

export = getIndexesPerformance;
