import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class disableIndexCommand extends commandBase {

    constructor(private indexName: string, private db: database, private location: databaseLocationSpecifier) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            name: this.indexName,
            ...this.location
            //TODO: clusterWide: this.clusterWide
        };
        
        const url = endpoints.databases.adminIndex.adminIndexesDisable + this.urlEncodeArgs(args);
        
        //tODO: report messages
        return this.post(url, null, this.db, { dataType: undefined });
    }
}

export = disableIndexCommand; 
