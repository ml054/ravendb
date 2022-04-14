/// <reference path="../../typings/tsd.d.ts" />
import endpoints from "endpoints";
import liveIOStatsWebSocketClient from "common/liveIOStatsWebSocketClient";

class serverWideLiveIOStatsWebSocketClient extends liveIOStatsWebSocketClient {

    constructor(onData: (data: Raven.Server.Utils.IoMetrics.IOMetricsResponse) => void,
                dateCutOff?: Date) {
        super(null, onData, dateCutOff);
    }

    get connectionDescription() {
        return "System-Wide Live I/O Stats";
    }

    protected webSocketUrlFactory() {
        return endpoints.global.adminIoMetrics.adminDebugIoMetricsLive;
    }
}

export = serverWideLiveIOStatsWebSocketClient;

