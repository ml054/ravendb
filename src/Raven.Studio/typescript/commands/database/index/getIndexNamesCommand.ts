import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";
import { shardingTodo } from "common/developmentHelper";

class getIndexNamesCommand extends commandBase {

    constructor(private db: database, private location: databaseLocationSpecifier = null) {
        super();
        shardingTodo("Danielle"); // TODO - location param should not be optional
    }

    execute(): JQueryPromise<string[]> {
        const args = {
            namesOnly: true,
            pageSize: 102,
            ...this.location
        };
        
        const url = endpoints.databases.index.indexes;
        return this.query(url, args, this.db, (x: resultsDto<string>) => x.Results)
            .fail((response: JQueryXHR) => this.reportError("Failed to get the database indexes", response.responseText, response.statusText));
    }
} 

export = getIndexNamesCommand;
