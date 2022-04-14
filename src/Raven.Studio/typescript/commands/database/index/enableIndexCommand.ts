import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class enableIndexCommand extends commandBase {

    constructor(private indexName: string, private db: database, private location: databaseLocationSpecifier) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            name: this.indexName,
            ...this.location
            //TODO: clusterWide: this.clusterWide - how is it related to shards?
        };
        
        const url = endpoints.databases.adminIndex.adminIndexesEnable + this.urlEncodeArgs(args);
        
        //TODO: report messages
        return this.post(url, null, this.db, { dataType: undefined });
    }
}

export = enableIndexCommand; 
