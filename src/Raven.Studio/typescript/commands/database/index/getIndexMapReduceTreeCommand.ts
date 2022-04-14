import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getIndexMapReduceTreeCommand extends commandBase {

    constructor(private db: database, private indexName: string, private documentIds: Array<string>) {
        super();
    }

    execute(): JQueryPromise<Raven.Server.Documents.Indexes.Debugging.ReduceTree[]> {
        const url = endpoints.databases.index.indexesDebug;
        const args =
        {
            docId: this.documentIds,
            name: this.indexName,
            op: "map-reduce-tree"
        };
        return this.query(url + this.urlEncodeArgs(args), null, this.db, x => x.Results)
            .fail((response: JQueryXHR) => this.reportError("Failed to load map reduce tree", response.responseText, response.statusText))
            .done((trees: Raven.Server.Documents.Indexes.Debugging.ReduceTree[]) => {
                if (!trees.length) {
                    const documents = this.documentIds.map(x => "'" + x + "'").join(",");
                    this.reportWarning("No results found for " + documents);
                }
            })
    }
} 

export = getIndexMapReduceTreeCommand;
