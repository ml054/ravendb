import commandBase from "commands/commandBase";
import endpoints from "endpoints";
import clusterTopology from "models/database/cluster/clusterTopology";

class getClusterTopologyCommand extends commandBase {

    constructor(private serverUrl?: string) {
        super();
    }

    execute(): JQueryPromise<clusterTopology> {
        
        const args = {
            url: window.location.origin
        };
        const url = endpoints.global.rachisAdmin.clusterTopology;

        return this.query(url, args["url"] ? args : null, null, dto => new clusterTopology(dto));
    }
}

export = getClusterTopologyCommand;
