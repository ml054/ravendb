import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getNextOperationId extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<number> {
        const url = this.db ? endpoints.databases.operations.operationsNextOperationId : endpoints.global.operationsServer.adminOperationsNextOperationId;
        return this.query(url, null, this.db, x => x.Id);
    }
}

export = getNextOperationId; 
