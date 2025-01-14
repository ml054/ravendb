import clusterDashboard = require("viewmodels/resources/clusterDashboard");
import abstractChartsWebsocketWidget = require("viewmodels/resources/widgets/abstractChartsWebsocketWidget");
import lineChart = require("models/resources/clusterDashboard/lineChart");
import serverTraffic = require("models/resources/widgets/serverTraffic");

interface trafficState {
    showWritesDetails: boolean;
    showDataWrittenDetails: boolean;
}

class trafficWidget extends abstractChartsWebsocketWidget<Raven.Server.Dashboard.Cluster.Notifications.TrafficWatchPayload, serverTraffic, void, trafficState> {

    view = require("views/resources/widgets/trafficWidget.html");
    
    showWritesDetails = ko.observable<boolean>(false);
    showDataWrittenDetails = ko.observable<boolean>(false);
    
    requestsChart: lineChart.lineChart<Raven.Server.Dashboard.Cluster.Notifications.TrafficWatchPayload>;
    avgRequestTimeChart: lineChart.lineChart<Raven.Server.Dashboard.Cluster.Notifications.TrafficWatchPayload>;
    writesChart: lineChart.lineChart<Raven.Server.Dashboard.Cluster.Notifications.TrafficWatchPayload>;
    dataWrittenChart: lineChart.lineChart<Raven.Server.Dashboard.Cluster.Notifications.TrafficWatchPayload>;
    
    constructor(controller: clusterDashboard) {
        super(controller);
        
        _.bindAll(this, "toggleWritesDetails", "toggleDataWrittenDetails");

        for (const node of this.controller.nodes()) {
            const stats = new serverTraffic(node.tag());
            this.nodeStats.push(stats);
        }
    }
    
    getType(): Raven.Server.Dashboard.Cluster.ClusterDashboardNotificationType {
        return "Traffic";
    }

    getState(): trafficState {
        return {
            showDataWrittenDetails: this.showDataWrittenDetails(),
            showWritesDetails: this.showWritesDetails()
        }
    }

    restoreState(state: trafficState) {
        this.showWritesDetails(state.showWritesDetails);
        this.showDataWrittenDetails(state.showDataWrittenDetails);
    }
    
    initCharts() {
        const requestsContainer = this.container.querySelector(".requests-chart");
        this.requestsChart = new lineChart.lineChart(requestsContainer, x => x.RequestsPerSecond, {
            grid: true,
            fillData: true,
            tooltipProvider: date => trafficWidget.tooltipContent(date),
            onMouseMove: date => this.onMouseMove(date)
        });

        const avgRequestTimeContainer = this.container.querySelector(".avg-request-time-chart");
        this.avgRequestTimeChart = new lineChart.lineChart(avgRequestTimeContainer, x => x.AverageRequestDuration, {
            grid: true,
            fillData: true,
            tooltipProvider: date => trafficWidget.tooltipContent(date),
            onMouseMove: date => this.onMouseMove(date)
        });
        
        const writesChartContainer = this.container.querySelector(".writes-chart");
        this.writesChart = new lineChart.lineChart(writesChartContainer,
            x => x.DocumentWritesPerSecond + x.AttachmentWritesPerSecond + x.CounterWritesPerSecond + x.TimeSeriesWritesPerSecond,
            {
                grid: true,
                fillData: true,
                tooltipProvider: date => trafficWidget.tooltipContent(date),
                onMouseMove: date => this.onMouseMove(date)
            });
        
        const dataWrittenContainer = this.container.querySelector(".data-written-chart");
        this.dataWrittenChart = new lineChart.lineChart(dataWrittenContainer,
            x => x.DocumentsWriteBytesPerSecond + x.AttachmentsWriteBytesPerSecond + x.CountersWriteBytesPerSecond + x.TimeSeriesWriteBytesPerSecond,
            {
                grid: true,
                fillData: true,
                tooltipProvider: date => trafficWidget.tooltipContent(date),
                onMouseMove: date => this.onMouseMove(date)
            });
        
        return [this.requestsChart, this.avgRequestTimeChart, this.writesChart, this.dataWrittenChart];
    }
    
    toggleWritesDetails() {
        this.showWritesDetails.toggle();

        this.controller.layout(true, "shift");
    }

    toggleDataWrittenDetails() {
        this.showDataWrittenDetails.toggle();

        this.controller.layout(true, "shift");
    }
}

export = trafficWidget;
