import commandBase from "commands/commandBase";
import database from "models/resources/database";
import appUrl from "common/appUrl";
import endpoints from "endpoints";
import genUtils from "common/generalUtils";
import { shardingTodo } from "common/developmentHelper";

class getDatabaseStatsCommand extends commandBase {

    constructor(private db: database, private location: databaseLocationSpecifier = null) {
        super();
        shardingTodo("Danielle"); // TODO - handle the location param, calling methods should pass either a known location or the 'getFirstLocation' info
    }

    execute(): JQueryPromise<Raven.Client.Documents.Operations.DatabaseStatistics> {
        const url = this.getQueryUrlFragment();
        const args = this.location;
        
        return this.query<Raven.Client.Documents.Operations.DatabaseStatistics>(url, args, this.db, null, null)
            .fail((response: JQueryXHR) => this.reportError(`Failed to get database statistics for ${genUtils.formatLocation(this.location)}`, response.responseText, response.statusText));
    }

    getQueryUrl(): string {
        return appUrl.forDatabaseQuery(this.db) + this.getQueryUrlFragment();
    }

    private getQueryUrlFragment(): string {
        return endpoints.databases.stats.stats;
    }
}

export = getDatabaseStatsCommand;
