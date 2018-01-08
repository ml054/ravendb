/// <reference path="../../../typings/tsd.d.ts"/>
import clusterTopology = require("models/database/cluster/clusterTopology");
import getClusterTopologyCommand = require("commands/database/cluster/getClusterTopologyCommand");
import changesContext = require("common/changesContext");

class clusterTopologyManager {

    static default = new clusterTopologyManager();

    topology = ko.observable<clusterTopology>();

    localNodeTag: KnockoutComputed<string>;
    localNodeUrl: KnockoutComputed<string>;
    
    currentTerm: KnockoutComputed<number>;
    votingInProgress: KnockoutComputed<boolean>;
    nodesCount: KnockoutComputed<number>;
    
    leader: string;
    leaderUrl: string;
    
    init(): JQueryPromise<clusterTopology> {
        return this.fetchTopology(true);
    }

    private fetchTopology(isFirst: boolean = false) {
        return new getClusterTopologyCommand()
            .execute()
            .done((topology: clusterTopology) => {
                this.topology(topology);
                if (isFirst){
                    this.leader = this.topology().leader();
                    this.leaderUrl = this.topology().leaderUrl();
                }
            });
    }

    constructor() {
        this.initObservables();
    }

    private getLeaderHostPart(): string{
        const leaderUrlLocal = this.leaderUrl;
        
        if (!leaderUrlLocal){
            return window.location.host;
        }
        
        const url = new URL(leaderUrlLocal);
        return url.host;
    }

    // setupGlobalNotifications() {
    //     const task = changesContext.default.connectServerWideNotificationCenter();
    //     task.done(() => {
    //         const serverWideClient = changesContext.default.serverNotifications();
    //
    //         serverWideClient.watchClusterTopologyChanges(e => {
    //             const tempClusterTopology = new clusterTopology(e);
    //             this.leaderUrl = tempClusterTopology.leaderUrl();
    //             if (this.leader != e.Leader) {
    //                 this.leader = e.Leader;
    //                 this.setupLeaderGlobalNotifications();
    //             }
    //         });
    //
    //         serverWideClient.watchReconnect(() => this.fetchTopology());
    //     });
    //
    //     this.setupLeaderGlobalNotifications();
    // }
    //

    /// Not sure about this.....
    setupGlobalNotifications() {
        const task = changesContext.default.connectServerWideNotificationCenter();

        task.done(() => {
            const serverWideClient = changesContext.default.serverNotifications();                

            serverWideClient.watchReconnect(() => this.fetchTopology());

            serverWideClient.watchClusterTopologyChanges(e => {

                console.log("NEW leader is: " + e.Leader);
                console.log("CURRENT leader is: " + this.leader);
                
                if (this.leader !== e.Leader) { 
                    
                    // 0. We have a new leader (e.Leader)  
                    //    Update details for the new Leader
                    this.leader = e.Leader;
                    this.leaderUrl = (new clusterTopology(e)).leaderUrl();
                   
                    if (window.location.host !== this.getLeaderHostPart()) {
                        // 1. Open special ws with the new leader url - only if I am Not leader...
                        this.setupLeaderGlobalNotifications();
                    }
                }
                else {
                    // 2. Register also for topology changes for the local server - Only if I am leader...
                    if (window.location.host === this.getLeaderHostPart()) {
                        this.onTopologyUpdated(e);
                    }
                }
            });
        });

        // Register for topology changes only if current open tab is Not the Leader
        if (window.location.host !== this.getLeaderHostPart()) {
            this.setupLeaderGlobalNotifications();
        }
    }

    setupLeaderGlobalNotifications() {
        const task = changesContext.default.connectLeaderNotificationCenter(this.getLeaderHostPart());
        
        task.done(() => {
            const leaderClient = changesContext.default.leaderNotifications();
            leaderClient.watchClusterTopologyChanges(e => this.onTopologyUpdated(e));
        });
    }

    private onTopologyUpdated(e: Raven.Server.NotificationCenter.Notifications.Server.ClusterTopologyChanged) {
        this.topology().updateWith(e);
    }

    private initObservables() {
        this.currentTerm = ko.pureComputed(() => {
            const topology = this.topology();
            return topology ? topology.currentTerm() : null;
        });
        
        this.localNodeTag = ko.pureComputed(() => {
            const topology = this.topology();
            return topology ? topology.localNodeTag() : null;
        });

        this.localNodeUrl = ko.pureComputed(() => {
            const localNode = _.find(this.topology().nodes(), x => x.tag() === this.localNodeTag());
            return localNode ? localNode.serverUrl() : null;
        });

        this.nodesCount = ko.pureComputed(() => {
            const topology = this.topology();
            return topology ? topology.nodes().length : 0;
        });

        this.votingInProgress = ko.pureComputed(() => {
            const topology = this.topology();
            if (!topology) {
                return false;
            }

            const leader = topology.leader();
            const isPassive = topology.nodeTag() === "?";
            return !leader && !isPassive;
        });
    }
}

export = clusterTopologyManager;
