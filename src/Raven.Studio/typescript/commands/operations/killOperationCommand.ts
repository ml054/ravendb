import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class killOperationCommand extends commandBase {

    constructor(private db: database, private taskId: number) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            id: this.taskId
        }
        const url = this.db ? endpoints.databases.operations.operationsKill :
            endpoints.global.operationsServer.adminOperationsKill;

        return this.post(url + this.urlEncodeArgs(args), null, this.db, { dataType: undefined });
    }
}

export = killOperationCommand;
