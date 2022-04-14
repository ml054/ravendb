import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class removeNodeFromClusterCommand extends commandBase {

    constructor(private nodeTag: string) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            nodeTag: this.nodeTag
        };
        const url = endpoints.global.rachisAdmin.adminClusterNode + this.urlEncodeArgs(args);

        return this.del<void>(url, null, null, { dataType: undefined });
    }
}

export = removeNodeFromClusterCommand;
