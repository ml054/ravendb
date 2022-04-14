import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getClusterNodeInfoCommand extends commandBase {

    constructor() {
        super();
    }

    execute(): JQueryPromise<Raven.Client.ServerWide.Commands.NodeInfo> {
        const url = endpoints.global.rachisAdmin.clusterNodeInfo;

        return this.query(url, null);
    }
}

export = getClusterNodeInfoCommand;
