import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class clearIndexErrorsCommand extends commandBase {
    constructor(private indexesNames: string[], private db: database, private location: databaseLocationSpecifier) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = this.getArgsToUse();
        const url = endpoints.databases.index.indexesErrors + this.urlEncodeArgs(args);

        return this.del<void>(url, null, this.db);
    }

    private getArgsToUse() {
        if (this.indexesNames) {
            return {
                name: this.indexesNames,
                ...this.location,
            }
        }
        
        return this.location;
    }
}

export = clearIndexErrorsCommand;
