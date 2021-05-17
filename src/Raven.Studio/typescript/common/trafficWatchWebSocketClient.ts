/// <reference path="../../typings/tsd.d.ts" />
import abstractWebSocketClient = require("common/abstractWebSocketClient");
import endpoints = require("endpoints");

class trafficWatchWebSocketClient extends abstractWebSocketClient<Raven.Client.Documents.Changes.TrafficWatchChangeBase> {

    private readonly onData: (data: Raven.Client.Documents.Changes.TrafficWatchChangeBase) => void;

    constructor(onData: (data: Raven.Client.Documents.Changes.TrafficWatchChangeBase) => void) {
        super(null);
        this.onData = onData;
    }

    get connectionDescription() {
        return "Traffic Watch";
    }

    protected webSocketUrlFactory() {
        return endpoints.global.trafficWatch.adminTrafficWatch;
    }

    get autoReconnect() {
        return true;
    }
    
    protected onMessage(e: Raven.Client.Documents.Changes.TrafficWatchChangeBase) {
        this.onData(e);
    }
}

export = trafficWatchWebSocketClient;
