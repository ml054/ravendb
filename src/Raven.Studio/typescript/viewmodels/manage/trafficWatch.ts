import viewModelBase = require("viewmodels/viewModelBase");
import moment = require("moment");
import fileDownloader = require("common/fileDownloader");
import textColumn = require("widgets/virtualGrid/columns/textColumn");
import eventsCollector = require("common/eventsCollector");
import columnPreviewPlugin = require("widgets/virtualGrid/columnPreviewPlugin");
import trafficWatchWebSocketClient = require("common/trafficWatchWebSocketClient");
import virtualGridController = require("widgets/virtualGrid/virtualGridController");
import generalUtils = require("common/generalUtils");

class trafficWatch extends viewModelBase {
    
    static maxBufferSize = 200000;
    
    private liveClient = ko.observable<trafficWatchWebSocketClient>();
    private allData = [] as Raven.Client.Documents.Changes.TrafficWatchChange[];
    private filteredData = [] as Raven.Client.Documents.Changes.TrafficWatchChange[];
    private gridController = ko.observable<virtualGridController<Raven.Client.Documents.Changes.TrafficWatchChange>>();
    private columnPreview = new columnPreviewPlugin<Raven.Client.Documents.Changes.TrafficWatchChange>();
    
    stats = {
        count: ko.observable<string>(),
        min: ko.observable<string>(),
        avg: ko.observable<string>(),
        max: ko.observable<string>()
    };
    
    filter = ko.observable<string>();

    private appendElementsTask: number;
    
    isBufferFull = ko.observable<boolean>();
    tailEnabled = ko.observable<boolean>(true);
    private duringManualScrollEvent = false;
    
    constructor() {
        super();
        
        this.filter.throttle(500).subscribe(() => this.filterEntries());
    }
    
    activate(args: any) {
        super.activate(args);
        this.updateHelpLink('EVEP6I');
    }

    deactivate() {
        super.deactivate();

        if (this.liveClient()) {
            this.liveClient().dispose();
        }
    }
    
    private filterEntries() {
        const filter = this.filter();
        if (filter) {
            this.filteredData = this.allData.filter(item => this.matchesFilter(item));
        } else {
            this.filteredData = this.allData.slice();
        }

        this.updateStats();
        
        this.gridController().reset(true);
    }
    
    private matchesFilter(item: Raven.Client.Documents.Changes.TrafficWatchChange) {
        const filter = this.filter();
        if (!filter) {
            return true;
        }
        const filterLowered = filter.toLocaleLowerCase();
        const uri = item.RequestUri.toLocaleLowerCase();
        return uri.includes(filterLowered);
    }
    
    private updateStats() {
        if (!this.filteredData.length) {
            this.stats.avg("n/a");
            this.stats.min("n/a");
            this.stats.max("n/a");
            this.stats.count("0");
        } else {
            let countForAvg = 0;
            let sum = 0;
            let min = this.filteredData[0].ElapsedMilliseconds;
            let max = this.filteredData[0].ElapsedMilliseconds;
            
            for (let i = 0; i < this.filteredData.length; i++) {
                const item = this.filteredData[i];
                
                if (item.ResponseStatusCode === 101) {
                    // it is websocket - don't include in stats
                    continue;
                }

                if (item.ElapsedMilliseconds < min) {
                    min = item.ElapsedMilliseconds;
                }

                if (item.ElapsedMilliseconds > max) {
                    max = item.ElapsedMilliseconds;
                }

                countForAvg++;
                sum += item.ElapsedMilliseconds;
            }
            
            this.stats.min(min.toLocaleString() + " ms");
            this.stats.max(max.toLocaleString() + " ms");
            this.stats.count(this.filteredData.length.toLocaleString());
            
            const avg = countForAvg ? generalUtils.formatNumberToStringFixed(sum * 1.0 / countForAvg, 2) + " ms" : "n/a"; 
            this.stats.avg(avg);
        }
    }
    
    compositionComplete() {
        super.compositionComplete();

        $('.traffic-watch [data-toggle="tooltip"]').tooltip();
        
        this.updateStats();

        const rowHighlightRules = {
            extraClass: (item: Raven.Client.Documents.Changes.TrafficWatchChange) => {
                const responseCode = item.ResponseStatusCode.toString();
                if (responseCode.startsWith("4")) {
                    return "bg-warning";
                } else if (responseCode.startsWith("5")) {
                    return "bg-danger";
                }
                return "";
            }
        };
        
        const grid = this.gridController();
        grid.headerVisible(true);
        grid.init((s, t) => this.fetchTraffic(s, t), () =>
            [
                new textColumn<Raven.Client.Documents.Changes.TrafficWatchChange>(grid, x => x.TimeStamp, "Timestamp", "20%", rowHighlightRules),
                new textColumn<Raven.Client.Documents.Changes.TrafficWatchChange>(grid, x => x.ResponseStatusCode, "Status", "8%", rowHighlightRules),
                new textColumn<Raven.Client.Documents.Changes.TrafficWatchChange>(grid, x => x.DatabaseName, "Database Name", "8%", rowHighlightRules),
                new textColumn<Raven.Client.Documents.Changes.TrafficWatchChange>(grid, x => x.ElapsedMilliseconds, "Duration", "8%", rowHighlightRules),
                new textColumn<Raven.Client.Documents.Changes.TrafficWatchChange>(grid, x => x.HttpMethod, "Method", "10%", rowHighlightRules),
                new textColumn<Raven.Client.Documents.Changes.TrafficWatchChange>(grid, x => x.RequestUri, "URI", "35%", rowHighlightRules)
            ]
        );

        this.columnPreview.install("virtual-grid", ".tooltip", 
            (item: Raven.Client.Documents.Changes.TrafficWatchChange, column: textColumn<Raven.Client.Documents.Changes.TrafficWatchChange>, 
             e: JQueryEventObject, onValue: (context: any) => void) => {
            if (column.header === "URI") {
                onValue(item.RequestUri);
            }
        });

        $(".traffic-watch .viewport").on("scroll", () => {
            if (!this.duringManualScrollEvent && this.tailEnabled()) {
                this.tailEnabled(false);
            }

            this.duringManualScrollEvent = false;
        });
        
        this.connectWebSocket();
    }

    private fetchTraffic(skip: number,  take: number): JQueryPromise<pagedResult<Raven.Client.Documents.Changes.TrafficWatchChange>> {
        return $.when({
            totalResultCount: this.filteredData.length,
            items: _.take(this.filteredData.slice(skip), take)
        });
    }
    
    connectWebSocket() {
        eventsCollector.default.reportEvent("traffic-watch", "connect");
        
        const ws = new trafficWatchWebSocketClient(data => this.onData(data));
        this.liveClient(ws);
    }
    
    private onData(data: Raven.Client.Documents.Changes.TrafficWatchChange) {
        if (this.allData.length === trafficWatch.maxBufferSize) {
            this.isBufferFull(true);
            this.pause();
            return;
        }
        
        this.allData.push(data);
        if (this.matchesFilter(data)) {
            this.filteredData.push(data);
        }
        
        if (!this.appendElementsTask) {
            this.appendElementsTask = setTimeout(() => this.onAppendPendingEntries(), 333);
        }
    }
    
    private onAppendPendingEntries() {
        this.appendElementsTask = null;
        
        this.updateStats();
        
        this.gridController().reset(false);
        
        if (this.tailEnabled()) {
            this.scrollDown();
        }
    }
    
    pause() {
        eventsCollector.default.reportEvent("traffic-watch", "pause");
        
        if (this.liveClient()) {
            this.liveClient().dispose();
            this.liveClient(null);
        }
    }
    
    resume() {
        this.connectWebSocket();
    }

    clear() {
        eventsCollector.default.reportEvent("traffic-watch", "clear");
        this.allData = [];
        this.filteredData = [];
        this.isBufferFull(false);
        this.gridController().reset(true);
        
        // set flag to true, since grid reset is async
        this.duringManualScrollEvent = true;
        this.tailEnabled(true);
        
        if (!this.liveClient()) {
            this.resume();
        }
    }

    exportToFile() {
        eventsCollector.default.reportEvent("traffic-watch", "export");

        const now = moment().format("YYYY-MM-DD HH-mm");
        fileDownloader.downloadAsJson(this.allData, "traffic-watch-" + now + ".json");
    }

    toggleTail() {
        this.tailEnabled.toggle();

        if (this.tailEnabled()) {
            this.scrollDown();
        }
    }

    private scrollDown() {
        this.duringManualScrollEvent = true;

        this.gridController().scrollDown();
    }

}

export = trafficWatch;
