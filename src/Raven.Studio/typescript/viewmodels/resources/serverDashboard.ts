import app = require("durandal/app");
import viewModelBase = require("viewmodels/viewModelBase");
import virtualGridController = require("widgets/virtualGrid/virtualGridController");
import hyperlinkColumn = require("widgets/virtualGrid/columns/hyperlinkColumn");
import textColumn = require("widgets/virtualGrid/columns/textColumn");
import generalUtils = require("common/generalUtils");
import trafficItem = require("models/resources/serverDashboard/trafficItem");
import databaseItem = require("models/resources/serverDashboard/databaseItem");
import indexingSpeed = require("models/resources/serverDashboard/indexingSpeed");
import machineResources = require("models/resources/serverDashboard/machineResources");
import driveUsage = require("models/resources/serverDashboard/driveUsage");
import clusterTopologyManager = require("common/shell/clusterTopologyManager");
import appUrl = require("common/appUrl");
import dashboardChart = require("models/resources/serverDashboard/dashboardChart");
import storagePieChart = require("models/resources/serverDashboard/storagePieChart");
import serverDashboardWebSocketClient = require("common/serverDashboardWebSocketClient");
import clusterNode = require("models/database/cluster/clusterNode");
import databasesManager = require("common/shell/databasesManager");
import createDatabase = require("viewmodels/resources/createDatabase");
import serverTime = require("common/helpers/database/serverTime");
import accessManager = require("common/shell/accessManager");

class machineResourcesSection {

    cpuChart: dashboardChart;
    memoryChart: dashboardChart;
    
    totalMemory: number;
    
    resources = ko.observable<machineResources>();

    init() {
        this.cpuChart = new dashboardChart("#cpuChart", {
            yMaxProvider: () => 100,
            topPaddingProvider: () => 2,
            tooltipProvider: data => machineResourcesSection.cpuTooltip(data)
        });

        this.memoryChart = new dashboardChart("#memoryChart", {
            yMaxProvider: () => this.totalMemory,
            topPaddingProvider: () => 2,
            tooltipProvider: data => machineResourcesSection.memoryTooltip(data, this.totalMemory)
        });
    }
    
    onResize() {
        this.cpuChart.onResize();
        this.memoryChart.onResize();
    }
    
    onData(data: Raven.Server.Dashboard.MachineResources) {
        this.totalMemory = data.TotalMemory;

        this.cpuChart.onData(moment.utc(data.Date).toDate(),
            [
                { key: "machine", value: data.MachineCpuUsage },
                { key: "process", value: data.ProcessCpuUsage }
            ]);
        this.memoryChart.onData(moment.utc(data.Date).toDate(),
            [
                { key: "machine", value: data.TotalMemory - data.AvailableMemory },
                { key: "process", value: data.ProcessMemoryUsage }
            ]);
        
        if (this.resources()) {
            this.resources().update(data);
        } else {
            this.resources(new machineResources(data));
            $('.dashboard-cpu-memory [data-toggle="tooltip"]').tooltip();
        }
    }
    
    
    private static cpuTooltip(data: dashboardChartTooltipProviderArgs) {
        if (data) {
            const date = moment(data.date).format(serverDashboard.timeFormat);
            const machine = data.values['machine'].toFixed(0) + "%";
            const process = data.values['process'].toFixed(0) + "%";
            return `<div>
                Time: <strong>${date}</strong><br />
                Machine CPU usage: <strong>${machine}</strong><br />
                Process CPU usage: <strong>${process}</strong>
                </div>`;
        }
        
        return null;
    }

    private static memoryTooltip(data: dashboardChartTooltipProviderArgs, totalMemory: number) {
        if (data) {
            const date = moment(data.date).format(serverDashboard.timeFormat);
            const physical = generalUtils.formatBytesToSize(totalMemory); 
            const machine = generalUtils.formatBytesToSize(data.values['machine']); 
            const process = generalUtils.formatBytesToSize(data.values['process']);
            return `<div>
                Time: <strong>${date}</strong><br />
                Usable physical memory: <strong>${physical}</strong><br />
                Machine memory usage: <strong>${machine}</strong><br />
                Process memory usage: <strong>${process}</strong>
                </div>`;
        }

        return null;
    }
}

class indexingSpeedSection {
    indexingChart: dashboardChart;
    reduceChart: dashboardChart;
    
    private table = [] as indexingSpeed[];
    private gridController = ko.observable<virtualGridController<indexingSpeed>>();

    totalIndexedPerSecond = ko.observable<number>(0);
    totalMappedPerSecond = ko.observable<number>(0);
    totalReducedPerSecond = ko.observable<number>(0);

    init() {
        this.indexingChart = new dashboardChart("#indexingChart", {
            tooltipProvider: data => indexingSpeedSection.indexingTooltip(data)
        });
        this.reduceChart = new dashboardChart("#reduceChart", {
            tooltipProvider: data => indexingSpeedSection.reduceTooltip(data)
        });
        
        const grid = this.gridController();

        grid.headerVisible(true);

        grid.init((s, t) => $.when({
            totalResultCount: this.table.length,
            items: this.table
        }), () => {
            return [
                //TODO:  new checkedColumn(true),
                new hyperlinkColumn<indexingSpeed>(grid, x => x.database(), x => appUrl.forIndexPerformance(x.database()), "Database", "30%"),
                new textColumn<indexingSpeed>(grid, x => x.indexedPerSecond() != null ? x.indexedPerSecond() : "n/a", "Indexed / sec", "15%", {
                    extraClass: item => item.indexedPerSecond() != null ? "" : "na"
                }),
                new textColumn<indexingSpeed>(grid, x => x.mappedPerSecond() != null ? x.mappedPerSecond() : "n/a", "Mapped / sec", "15%", {
                    extraClass: item => item.mappedPerSecond() != null ? "" : "na"
                }),
                new textColumn<indexingSpeed>(grid, x => x.reducedPerSecond() != null ? x.reducedPerSecond() : "n/a", "Entries reduced / sec", "15%", {
                    extraClass: item => item.reducedPerSecond() != null ? "" : "na"
                })
            ];
        });
    }
    
    onResize() {
        this.indexingChart.onResize();
        this.reduceChart.onResize();
    }
    
    private static indexingTooltip(data: dashboardChartTooltipProviderArgs) {
        if (data) {
            const date = moment(data.date).format(serverDashboard.timeFormat);
            const indexed = data.values['indexing'];
            return `<div>
                Time: <strong>${date}</strong><br />
                # Documents indexed/s: <strong>${indexed.toLocaleString()}</strong>
                </div>`;
        }
        
        return null;
    }
    
    private static reduceTooltip(data: dashboardChartTooltipProviderArgs) {
        if (data) {
            const date = moment(data.date).format(serverDashboard.timeFormat);
            const map = data.values['map'];
            const reduce = data.values['reduce'];
            return `<div>
                Time: <strong>${date}</strong><br />
                # Documents mapped/s: <strong>${map.toLocaleString()}</strong><br />
                # Mapped entries reduced/s: <strong>${reduce.toLocaleString()}</strong>
                </div>`;
        }
        return null;
    }
    
    onData(data: Raven.Server.Dashboard.IndexingSpeed) {
        const items = data.Items;
        items.sort((a, b) => generalUtils.sortAlphaNumeric(a.Database, b.Database));

        const newDbs = items.map(x => x.Database);
        const oldDbs = this.table.map(x => x.database());

        const removed = _.without(oldDbs, ...newDbs);
        removed.forEach(dbName => {
            const matched = this.table.find(x => x.database() === dbName);
            _.pull(this.table, matched);
        });

        items.forEach(incomingItem => {
            const matched = this.table.find(x => x.database() === incomingItem.Database);
            if (matched) {
                matched.update(incomingItem);
            } else {
                this.table.push(new indexingSpeed(incomingItem));
            }
        });

        this.updateTotals();
        
        this.indexingChart.onData(moment.utc(data.Date).toDate(), [{
            key: "indexing", value: this.totalIndexedPerSecond() 
        }]);
        
        this.reduceChart.onData(moment.utc(data.Date).toDate(), [
            { key: "map", value: this.totalMappedPerSecond() },
            { key: "reduce", value: this.totalReducedPerSecond() }
        ]);

        this.gridController().reset(false);
    }

    private updateTotals() {
        let totalIndexed = 0;
        let totalMapped = 0;
        let totalReduced = 0;

        this.table.forEach(item => {
            totalIndexed += item.indexedPerSecond() || 0;
            totalMapped += item.mappedPerSecond() || 0;
            totalReduced += item.reducedPerSecond() || 0;
        });

        this.totalIndexedPerSecond(totalIndexed);
        this.totalMappedPerSecond(totalMapped);
        this.totalReducedPerSecond(totalReduced);
    }
}

class databasesSection {
    private table = [] as databaseItem[];
    private gridController = ko.observable<virtualGridController<databaseItem>>();
    
    totalOfflineDatabases = ko.observable<number>(0);
    totalOnlineDatabases = ko.observable<number>(0);
    totalDatabases: KnockoutComputed<number>;
    
    constructor() {
        this.totalDatabases = ko.pureComputed(() => this.totalOnlineDatabases() + this.totalOfflineDatabases());
    }
    
    init() {
        const grid = this.gridController();

        grid.headerVisible(true);

        grid.init((s, t) => $.when({
            totalResultCount: this.table.length,
            items: this.table
        }), () => {
            return [
                new hyperlinkColumn<databaseItem>(grid, x => x.database(), x => appUrl.forDocuments(null, x.database()), "Database", "30%"), 
                new textColumn<databaseItem>(grid, x => x.documentsCount(), "Docs #", "25%"),
                new textColumn<databaseItem>(grid, 
                        x => x.indexesCount() + ( x.erroredIndexesCount() ? ' (<span class=\'text-danger\'>' + x.erroredIndexesCount() + '</span>)' : '' ), 
                        "Index # (Error #)", 
                        "20%",
                        {
                            useRawValue: () => true
                        }),
                new textColumn<databaseItem>(grid, x => x.alertsCount(), "Alerts #", "12%", {
                    extraClass: item => item.alertsCount() ? 'has-alerts' : ''
                }), 
                new textColumn<databaseItem>(grid, x => x.replicationFactor(), "Replica factor", "12%")
            ];
        });
    }
    
    onData(data: Raven.Server.Dashboard.DatabasesInfo) {
        const items = data.Items;
        items.sort((a, b) => generalUtils.sortAlphaNumeric(a.Database, b.Database));

        const newDbs = items.map(x => x.Database);
        const oldDbs = this.table.map(x => x.database());

        const removed = _.without(oldDbs, ...newDbs);
        removed.forEach(dbName => {
            const matched = this.table.find(x => x.database() === dbName);
            _.pull(this.table, matched);
        });

        items.forEach(incomingItem => {
            const matched = this.table.find(x => x.database() === incomingItem.Database);
            if (matched) {
                matched.update(incomingItem);
            } else {
                this.table.push(new databaseItem(incomingItem));
            }
        });
        
        this.updateTotals();
        
        this.gridController().reset(false);
    }
    
    private updateTotals() {
        let totalOnline = 0;
        let totalOffline = 0;
        
        this.table.forEach(item => {
            if (item.online()) {
                totalOnline++;
            } else {
                totalOffline++;
            }
        });
        
        this.totalOnlineDatabases(totalOnline);
        this.totalOfflineDatabases(totalOffline);
    }
}

class trafficSection {
    private sizeFormatter = generalUtils.formatBytesToSize;
    
    private table = [] as trafficItem[];
    private trafficChart: dashboardChart;

    private gridController = ko.observable<virtualGridController<trafficItem>>();
    
    totalRequestsPerSecond = ko.observable<number>(0);
    totalWritesPerSecond = ko.observable<number>(0);
    totalDataWritesPerSecond = ko.observable<number>(0);
    
    init()  {
        const grid = this.gridController();

        grid.headerVisible(true);
        
        grid.init((s, t) => $.when({
            totalResultCount: this.table.length,
            items: this.table
        }), () => {
            return [
                //TODO: new checkedColumn(true),
                new hyperlinkColumn<trafficItem>(grid, x => x.database(), x => appUrl.forTrafficWatch(x.database()), "Database", "30%"),
                new textColumn<trafficItem>(grid, x => x.requestsPerSecond(), "Requests / s", "20%"),
                new textColumn<trafficItem>(grid, x => x.writesPerSecond(), "Writes / s", "25%"),
                new textColumn<trafficItem>(grid, x => this.sizeFormatter(x.dataWritesPerSecond()), "Data written / s", "25%")
            ];
        });
        
        this.trafficChart = new dashboardChart("#trafficChart", {
            useSeparateYScales: true,
            topPaddingProvider: key => {
                switch (key) {
                    case "written":
                        return 30;
                    case "writes":
                        return 20;
                    default:
                        return 5;
                }
            },
            tooltipProvider: data => this.trafficTooltip(data)
        });
    }
    
    onResize() {
        this.trafficChart.onResize();
        this.gridController().reset(true);
    }

    private trafficTooltip(data: dashboardChartTooltipProviderArgs) {
        if (data) {
            const date = moment(data.date).format(serverDashboard.timeFormat);
            const requests = data.values['requests'];
            const writes = data.values['writes'];
            const written = data.values['written'];

            return `<div>
                Time: <strong>${date}</strong><br />
                Requests/s: <strong>${requests.toLocaleString()}</strong><br />
                Writes/s: <strong>${writes.toLocaleString()}</strong><br />
                Data Written/s: <strong>${this.sizeFormatter(written)}</strong>
                </div>`;
        }
        return null;
    }
    
    onData(data: Raven.Server.Dashboard.TrafficWatch) {
        const items = data.Items;
        items.sort((a, b) => generalUtils.sortAlphaNumeric(a.Database, b.Database));

        const newDbs = items.map(x => x.Database);
        const oldDbs = this.table.map(x => x.database());
        
        const removed = _.without(oldDbs, ...newDbs);
        removed.forEach(dbName => {
           const matched = this.table.find(x => x.database() === dbName);
           _.pull(this.table, matched);
        });
        
        items.forEach(incomingItem => {
            const matched = this.table.find(x => x.database() === incomingItem.Database);
            if (matched) {
                matched.update(incomingItem);
            } else {
                this.table.push(new trafficItem(incomingItem));
            }
        });
        
        this.updateTotals();
        
        this.trafficChart.onData(moment.utc(data.Date).toDate(), [{
            key: "writes",
            value: this.totalWritesPerSecond()
        }, {
            key: "written",
            value: this.totalDataWritesPerSecond()
        },{
            key: "requests",
            value: this.totalRequestsPerSecond()
        }]);
        
        this.gridController().reset(false);
    }
    
    private updateTotals() {
        let totalRequests = 0;
        let writesPerSecond = 0;
        let dataWritesPerSecond = 0;

        this.table.forEach(item => {
            totalRequests += item.requestsPerSecond();
            writesPerSecond += item.writesPerSecond();
            dataWritesPerSecond += item.dataWritesPerSecond();
        });

        this.totalRequestsPerSecond(totalRequests);
        this.totalWritesPerSecond(writesPerSecond);
        this.totalDataWritesPerSecond(dataWritesPerSecond);
    }
}

class driveUsageSection {
    private table = ko.observableArray<driveUsage>();
    private storageChart: storagePieChart;
    
    totalDocumentsSize = ko.observable<number>(0);
    
    init() {
        this.storageChart = new storagePieChart("#storageChart");
    }
    
    onResize() {
        this.table().forEach(item => {
            item.gridController().reset(true);
        });
        
        this.storageChart.onResize();
    }
    
    onData(data: Raven.Server.Dashboard.DrivesUsage) {
        const items = data.Items;

        this.updateChart(data);

        const newMountPoints = items.map(x => x.MountPoint);
        const oldMountPoints = this.table().map(x => x.mountPoint());

        const removed = _.without(oldMountPoints, ...newMountPoints);
        removed.forEach(name => {
            const matched = this.table().find(x => x.mountPoint() === name);
            this.table.remove(matched);
        });

        items.forEach(incomingItem => {
            const matched = this.table().find(x => x.mountPoint() === incomingItem.MountPoint);
            if (matched) {
                matched.update(incomingItem);
            } else {
                const usage = new driveUsage(incomingItem, this.storageChart.getColorProvider());
                this.table.push(usage);
            }
        });

        this.updateTotals();
        
    }
    
    private updateChart(data: Raven.Server.Dashboard.DrivesUsage) {
        const cache = new Map<string, number>();

        // group by database size
        data.Items.forEach(mountPointUsage => {
            mountPointUsage.Items.forEach(item => {
                if (cache.has(item.Database)) {
                    cache.set(item.Database, item.Size + cache.get(item.Database));
                } else {
                    cache.set(item.Database, item.Size);
                }
            });
        });
        
        const result = [] as Raven.Server.Dashboard.DatabaseDiskUsage[];
        
        cache.forEach((value, key) => {
            result.push({
                Database: key,
                Size: value
            });
        });

        this.storageChart.onData(result);
    }
    
    private updateTotals() {
        this.totalDocumentsSize(_.sum(this.table().map(x => x.totalDocumentsSpaceUsed())));
    }
}

class serverDashboard extends viewModelBase {
    
    static readonly dateFormat = generalUtils.dateFormat;
    static readonly timeFormat = "h:mm:ss A";
    liveClient = ko.observable<serverDashboardWebSocketClient>();
    
    clusterManager = clusterTopologyManager.default;
    accessManager = accessManager.default.dashboardView;
    
    formattedUpTime: KnockoutComputed<string>;
    formattedStartTime: KnockoutComputed<string>;
    node: KnockoutComputed<clusterNode>;
    sizeFormatter = generalUtils.formatBytesToSize;

    usingHttps = location.protocol === "https:";

    certificatesUrl = appUrl.forCertificates();
    
    trafficSection = new trafficSection();
    databasesSection = new databasesSection();
    indexingSpeedSection = new indexingSpeedSection();
    machineResourcesSection = new machineResourcesSection();
    driveUsageSection = new driveUsageSection();
    
    noDatabases = ko.pureComputed(() => databasesManager.default.databases().length === 0);

    constructor() {
        super();

        this.formattedUpTime = ko.pureComputed(() => {
            const startTime = serverTime.default.startUpTime();
            if (!startTime) {
                return "a few seconds";
            }

            return generalUtils.formatDurationByDate(startTime, true);
        });

        this.formattedStartTime = ko.pureComputed(() => {
            const start = serverTime.default.startUpTime();
            return start ? start.local().format(serverDashboard.dateFormat) : "";
        });

        this.node = ko.pureComputed(() => {
            const topology = this.clusterManager.topology();
            const nodeTag = topology.nodeTag();
            return topology.nodes().find(x => x.tag() === nodeTag);
        });
    }

    compositionComplete() {
        super.compositionComplete();

        this.initSections();
        
        this.enableLiveView();
    }

    private initSections() {
        this.trafficSection.init();
        this.databasesSection.init();
        this.indexingSpeedSection.init();
        this.machineResourcesSection.init();
        this.driveUsageSection.init();
        
        this.registerDisposableHandler($(window), "resize", _.debounce(() => this.onResize(), 700));
    }
    
    private onResize() {
        this.trafficSection.onResize();
        this.indexingSpeedSection.onResize();
        this.machineResourcesSection.onResize();
        this.driveUsageSection.onResize();
    }
    
    deactivate() {
        super.deactivate();
        
        if (this.liveClient()) {
            this.liveClient().dispose();
        }
    }
    
    private enableLiveView() {
        
        const dbCreator = (name: string, docCount: number, indexCount: number) => {
            return {
                AlertsCount: 0,
                Database: name,
                Disabled: false,
                DocumentsCount: docCount,
                ErroredIndexesCount: 0,
                IndexesCount: indexCount,
                Irrelevant: false,
                Online: true,
                ReplicationFactor: 1
            } as Raven.Server.Dashboard.DatabaseInfoItem;
        };
        
        const dbInfo = {
            Type: "DatabasesInfo",
            Items: [
                dbCreator("DrugRequests", 5503203, 20),
                dbCreator("DutySchedule", 1572864, 5),
                dbCreator("Equipment", 1048576, 7),
                dbCreator("Hospitalizations", 14680064, 30),
                dbCreator("MedicalHistory", 24793845, 2),
                dbCreator("Prescriptions", 7864320, 7),
                dbCreator("StaffAndWages", 524288, 9)
            ]
            
        } as Raven.Server.Dashboard.DatabasesInfo;
        
        const resourcesUsage = {
             Date: null,
            Type: "MachineResources",
            TotalMemory: 64 * 1024 * 1023 * 1023,
            ProcessCpuUsage: 23,
            SystemCommitLimit: 72 * 1023 * 1023 * 1023,
            MachineCpuUsage: 57,
            AvailableMemory:  12 * 1024 * 1023 * 1023,
            
            IsWindows: true,
            IsLowMemory: false,
            CommitedMemory: 1024,
            ProcessMemoryUsage: 11.2 * 1024 * 1023 * 1023
            
            
        }as Raven.Server.Dashboard.MachineResources;
        
        const driveUsage = {
            Date: null,
            Type: "DriveUsage",
            Items: [
                {
                    FreeSpace: 133.08 * 1024* 1024*1024,
                    FreeSpaceLevel: "High",
                    MountPoint: "",
                    TotalCapacity: 502.08 * 1024* 1024*1024,
                    VolumeLabel: "C:\\",
                    Items: [
                        {   Database: "DrugRequests", Size: 20.93 * 1024 * 1024 * 1024 },
                        {   Database: "DutySchedule", Size: 5.98 * 1024 * 1024 * 1024 },
                        {   Database: "Equipment", Size: 3.98 * 1024 * 1024 * 1024 },
                        {   Database: "Hospitalizations", Size: 55.83 * 1024 * 1024 * 1024 },
                        {   Database: "MedicalHistory", Size: 92.85 * 1024 * 1024 * 1024 },
                        {   Database: "Prescriptions", Size: 48.85 * 1024 * 1024 * 1024 },
                        {   Database: "StaffAndWages", Size: 1.99 * 1024 * 1024 * 1024 },
                    ]
                }
            ]
        } as Raven.Server.Dashboard.DrivesUsage;
        
        const requests = {
            Date: null,
            Type: "TrafficWatch",
            Items: [
                {  Database: "DrugRequests", RequestsPerSecond: 30, WritesPerSecond: 35, WriteBytesPerSecond: 280 * 1024  },
                {  Database: "DutySchedule", RequestsPerSecond: 15, WritesPerSecond: 142, WriteBytesPerSecond: 1100 * 1024  },
                {  Database: "Equipment", RequestsPerSecond: 52, WritesPerSecond: 21, WriteBytesPerSecond: 168 * 1024  },
                {  Database: "Hospitalizations", RequestsPerSecond: 25, WritesPerSecond: 0, WriteBytesPerSecond: 0 * 1024  },
                {  Database: "MedicalHistory", RequestsPerSecond: 84, WritesPerSecond: 22, WriteBytesPerSecond: 176 * 1024  },
                {  Database: "Prescriptions", RequestsPerSecond: 191, WritesPerSecond: 0, WriteBytesPerSecond: 280 * 1024  },
                {  Database: "StaffAndWages", RequestsPerSecond: 2, WritesPerSecond: 0, WriteBytesPerSecond: 280 * 1024  },
            ]
        } as Raven.Server.Dashboard.TrafficWatch;
        
        const indexing = {
            Type: "IndexingSpeed",
            Date: null,
            Items: [
                { Database: "DrugRequests", IndexedPerSecond: 139, MappedPerSecond: 42, ReducedPerSecond: 14 },
                { Database: "DutySchedule", IndexedPerSecond: 28, MappedPerSecond: 7, ReducedPerSecond: 17 },
                { Database: "Equipment", IndexedPerSecond: 42, MappedPerSecond: 5, ReducedPerSecond: 1 },
                { Database: "Hospitalizations", IndexedPerSecond: 1, MappedPerSecond: 0, ReducedPerSecond: 0 },
                { Database: "MedicalHistory", IndexedPerSecond: 232, MappedPerSecond: 66, ReducedPerSecond: 96 },
                { Database: "Prescriptions", IndexedPerSecond: 942, MappedPerSecond: 1214, ReducedPerSecond: 240 },
                { Database: "StaffAndWages", IndexedPerSecond: 25, MappedPerSecond: 0, ReducedPerSecond: 0 },
            ]
        } as Raven.Server.Dashboard.IndexingSpeed;
        
        
        const update = () => {
            
            const date = new Date();
            requests.Date = moment.utc(date).format();
            resourcesUsage.Date = moment.utc(date).format();
            indexing.Date = moment.utc(date).format();
            
            requests.Items.forEach(item => {
                const diffRequests =Math.ceil(item.RequestsPerSecond * 0.1);
                item.RequestsPerSecond += _.random(-diffRequests, diffRequests);
                item.RequestsPerSecond = Math.max(0, item.RequestsPerSecond);


                const diffWrites =Math.ceil(item.WritesPerSecond * 0.1);
                item.WritesPerSecond += _.random(-diffWrites, diffWrites);
                item.WritesPerSecond = Math.max(0, item.WritesPerSecond);
                
                const diffDataWritten = Math.ceil(item.WriteBytesPerSecond * 0.1);
                item.WriteBytesPerSecond += _.random(-diffDataWritten, diffDataWritten);
                item.WriteBytesPerSecond = Math.max(0, item.WriteBytesPerSecond);
                
            });
            
            resourcesUsage.ProcessCpuUsage += _.random(-5, 5);
            resourcesUsage.MachineCpuUsage += _.random(-5, 5);
            resourcesUsage.MachineCpuUsage = Math.max(0, Math.min(100, resourcesUsage.MachineCpuUsage));
            resourcesUsage.ProcessCpuUsage = Math.max(0, Math.min(resourcesUsage.MachineCpuUsage, resourcesUsage.ProcessCpuUsage));
            
            
            indexing.Items.forEach(item => {
                const diff1 =Math.ceil(item.IndexedPerSecond * 0.1);
                item.IndexedPerSecond += _.random(-diff1, diff1);
                item.IndexedPerSecond = Math.max(0, item.IndexedPerSecond);

                const diff2 =Math.ceil(item.MappedPerSecond * 0.1);
                item.MappedPerSecond += _.random(-diff2, diff2);
                item.MappedPerSecond = Math.max(0, item.MappedPerSecond);

                const diff3 =Math.ceil(item.ReducedPerSecond * 0.1);
                item.ReducedPerSecond += _.random(-diff3, diff3);
                item.ReducedPerSecond = Math.max(0, item.ReducedPerSecond);
            });
            
            this.onData(dbInfo);
            this.onData(driveUsage);
            this.onData(requests);
            this.onData(resourcesUsage);
            this.onData(indexing);
        };
        
        
        setInterval(update, 1000);
        
        //this.liveClient(new serverDashboardWebSocketClient(d => this.onData(d)));
    }

    private onData(data: Raven.Server.Dashboard.AbstractDashboardNotification) {
        switch (data.Type) {
            case "DriveUsage":
                this.driveUsageSection.onData(data as Raven.Server.Dashboard.DrivesUsage);
                break;
            case "MachineResources":
                this.machineResourcesSection.onData(data as Raven.Server.Dashboard.MachineResources);
                break;
            case "TrafficWatch":
                this.trafficSection.onData(data as Raven.Server.Dashboard.TrafficWatch);
                break;
            case "DatabasesInfo":
                this.databasesSection.onData(data as Raven.Server.Dashboard.DatabasesInfo);
                break;
            case "IndexingSpeed":
                this.indexingSpeedSection.onData(data as Raven.Server.Dashboard.IndexingSpeed);
                break;
            default:
                throw new Error("Unhandled notification type: " + data.Type);
        }
    }
    
    newDatabase() {
        const createDbView = new createDatabase("newDatabase");
        app.showBootstrapDialog(createDbView);
    }
}

export = serverDashboard;
