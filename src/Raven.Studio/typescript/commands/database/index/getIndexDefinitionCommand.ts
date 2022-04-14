import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getIndexDefinitionCommand extends commandBase {

    constructor(private indexName: string, private db: database, private location: databaseLocationSpecifier) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Indexes.IndexDefinition> {
        const args = {
            name: this.indexName,
            ...this.location
        };
        
        const url = endpoints.databases.index.indexes;

        const extractor = (results: resultsDto<Raven.Client.Documents.Indexes.IndexDefinition>) =>
            results.Results && results.Results.length ? results.Results[0] : null;
        
        return this.query(url, args, this.db, extractor);
    }
}

export = getIndexDefinitionCommand;
