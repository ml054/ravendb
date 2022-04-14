import commandBase from "commands/commandBase";
import database from "models/resources/database";
import collectionsStats from "models/database/documents/collectionsStats";
import endpoints from "endpoints";

class getCollectionsStatsCommand extends commandBase {

    constructor(private ownerDb: database) {
        super();

        if (!this.ownerDb) {
            throw new Error("Must specify a database.");
        }
    }

    execute(): JQueryPromise<collectionsStats> {
        const finalResult = $.Deferred<collectionsStats>();
        this.query<Raven.Client.Documents.Operations.CollectionStatistics>(endpoints.databases.collections.collectionsStats, null, this.ownerDb)
            .done(results => {
                const stats = new collectionsStats(results, this.ownerDb);
                finalResult.resolve(stats);
            })
            .fail((response) => {
                this.reportError("Can't fetch collection stats", response.responseText, response.statusText);
                finalResult.reject(response.responseText);
            });

        return finalResult;
    }
}

export = getCollectionsStatsCommand;
