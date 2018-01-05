import clusterNode = require("models/database/cluster/clusterNode");
import genUtils = require("common/generalUtils");

class clusterTopology {
    leader = ko.observable<string>();
    leaderUrl = ko.observable<string>();
    leaderTag = ko.observable<string>();
    
    nodeTag = ko.observable<string>();
    localNodeTag: KnockoutComputed<string>;
    
    currentTerm = ko.observable<number>();    
    nodes = ko.observableArray<clusterNode>([]);
    
    membersCount: KnockoutComputed<number>;
    promotablesCount: KnockoutComputed<number>;
    watchersCount: KnockoutComputed<number>;

    constructor(dto: Raven.Server.NotificationCenter.Notifications.Server.ClusterTopologyChanged) {
        this.leader(dto.Leader);
        this.nodeTag(dto.NodeTag);
        this.currentTerm(dto.CurrentTerm);

        const topologyDto = dto.Topology;

        const members = this.mapNodes("Member", topologyDto.Members, dto.Status);
        const promotables = this.mapNodes("Promotable", topologyDto.Promotables, dto.Status);
        const watchers = this.mapNodes("Watcher", topologyDto.Watchers, dto.Status);

        this.nodes(_.concat<clusterNode>(members, promotables, watchers));
        this.nodes(_.sortBy(this.nodes(), x => x.tag().toUpperCase()));

        this.updateAssignedCores(dto.NodeLicenseDetails);
        
        this.membersCount = ko.pureComputed(() => {
            const nodes = this.nodes();
            return nodes.filter(x => x.type() === "Member").length;
        });

        this.promotablesCount = ko.pureComputed(() => {
            const nodes = this.nodes();
            return nodes.filter(x => x.type() === "Promotable").length;
        });

        this.watchersCount = ko.pureComputed(() => {
            const nodes = this.nodes();
            return nodes.filter(x => x.type() === "Watcher").length;
        });

        this.nodes().forEach(node => {      
            node.isLeader(node.tag() === this.leader());
            
            if (node.isLeader()) {
                this.leaderUrl(node.serverUrl()); 
                this.leaderTag(node.tag());
            }            
        });
        
        this.localNodeTag = ko.pureComputed(() => {
            let localHostName = window.location.hostname;
            localHostName = genUtils.isLocalhostIpAddress(localHostName) ? 'localhost' : localHostName;
            const localHostPort = window.location.port;
            const localHost = localHostName + ':' + localHostPort;

            for (let i = 0; i < this.nodes().length; i++) {
                const nodeItem = this.nodes()[i];

                let nodeHostName = (new URL(nodeItem.serverUrl())).hostname;
                nodeHostName = genUtils.isLocalhostIpAddress(nodeHostName) ? 'localhost' : nodeHostName;
                const nodeHostPort = (new URL(nodeItem.serverUrl())).port;
                const nodeHost = nodeHostName + ':' + nodeHostPort;

                if (nodeHost === localHost) {
                    return nodeItem.tag();
                }
            }                   
                        
            return '?'; 
        });
    }    
        
    private mapNodes(type: clusterNodeType, dict: System.Collections.Generic.Dictionary<string, string>,
        status: { [key: string]: Raven.Client.Http.NodeStatus; }): Array<clusterNode> {
        return _.map(dict, (v, k) => {
            // node statuses are available for all nodes except current
            const nodeStatus = status ? status[k] : null;
            const connected = nodeStatus ? nodeStatus.Connected : true;
            const errorDetails = connected ? null : nodeStatus.ErrorDetails;
            return clusterNode.for(k, v, type, connected, errorDetails);
        });
    }

    updateWith(incomingChanges: Raven.Server.NotificationCenter.Notifications.Server.ClusterTopologyChanged) {
        const newTopology = incomingChanges.Topology;

        const existingNodes = this.nodes();
        const newNodes = _.concat<clusterNode>(
            this.mapNodes("Member", newTopology.Members, incomingChanges.Status),
            this.mapNodes("Promotable", newTopology.Promotables, incomingChanges.Status),
            this.mapNodes("Watcher", newTopology.Watchers, incomingChanges.Status)
        );
        const newServerUrls = new Set<string>(newNodes.map(x => x.serverUrl()));

        if (existingNodes.length > 1) {
            const toDelete = existingNodes.filter(x => !newServerUrls.has(x.serverUrl()));
            toDelete.forEach(x => this.nodes.remove(x));
        }

        newNodes.forEach(node => {
            node.isLeader(node.tag() === incomingChanges.Leader);

            if (node.isLeader()) {
                this.leaderUrl(node.serverUrl());
                this.leaderTag(node.tag());
            }

            const matchedNode = existingNodes.find(x => x.serverUrl() === node.serverUrl());           
                        
            if (matchedNode) {
                matchedNode.updateWith(node);
            } else {
                const locationToInsert = _.sortedIndexBy(this.nodes(), node, item => item.tag().toLowerCase());
                this.nodes.splice(locationToInsert, 0, node);
            }
        });

        this.updateAssignedCores(incomingChanges.NodeLicenseDetails);
        this.nodeTag(incomingChanges.NodeTag);
                
        this.leader(incomingChanges.Leader);
        this.currentTerm(incomingChanges.CurrentTerm);
    }

    private updateAssignedCores(nodeLicenseDetails: { [key: string]: Raven.Server.Commercial.DetailsPerNode; }) {
        if (!nodeLicenseDetails)
            return;

        _.forOwn(nodeLicenseDetails, (detailsPerNode, nodeTag) => {
            const node = this.nodes().find(x => x.tag() === nodeTag);
            if (!node) {
                return;
            }

            node.utilizedCores(detailsPerNode.UtilizedCores);
            node.numberOfCores(detailsPerNode.NumberOfCores);
            node.installedMemoryInGb(detailsPerNode.InstalledMemoryInGb);
            node.usableMemoryInGb(detailsPerNode.UsableMemoryInGb);
        });
    }
}

export = clusterTopology;
