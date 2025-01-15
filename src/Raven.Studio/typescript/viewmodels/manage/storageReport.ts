import viewModelBase = require("viewmodels/viewModelBase");
import getSystemStorageReportCommand = require("commands/resources/getSystemStorageReportCommand");
import generalUtils = require("common/generalUtils");
import serverStorageReportItem = require("models/database/status/serverStorageReportItem");
import { select, Selection, pointer } from 'd3-selection';
import { descending } from "d3-array";
import "d3-transition";
import { treemap, hierarchy, HierarchyNode, HierarchyRectangularNode } from "d3-hierarchy";

type positionAndSizes = {
    dx: number,
    dy: number,
    x: number,
    y: number
}


//TODO: avoid any in this file
export class storageReport extends viewModelBase {

    view = require("views/manage/storageReport.html");

    static readonly animationLength = 200;

    private rawData: detailedSystemStorageReportItemDto;
    private currentPath = ko.observable<serverStorageReportItem[]>();
    private root: serverStorageReportItem;
    private node = ko.observable<serverStorageReportItem>();
    private svg: Selection<any, unknown, HTMLElement, any>;
    private tooltip: Selection<null, unknown, HTMLElement, any>;

    private w: number;
    private h: number;

    private transitioning = false;

    showPagesColumn: KnockoutObservable<boolean>;
    showEntriesColumn: KnockoutObservable<boolean>;
    showTempFiles: KnockoutObservable<boolean>;

    constructor() {
        super();
        this.bindToCurrentInstance("onClick");
    }

    activate(args: any) {
        super.activate(args);

        this.initObservables();

        return new getSystemStorageReportCommand()
            .execute()
            .done(result => {
                this.rawData = result;
            });
    }

    compositionComplete() {
        super.compositionComplete();
        this.processData();
        this.initGraph();
        this.draw(undefined);
    }

    private initObservables() {

        this.showEntriesColumn = ko.pureComputed(() => {
            const node = this.node();
            if (!node) {
                return false;
            }
            return !!node.internalChildren.find(x => x.type === "table" || x.type === "tree");
        });

        this.showPagesColumn = ko.pureComputed(() => {
            const node = this.node();
            if (!node) {
                return false;
            }
            return !!node.internalChildren.find(x => x.type === "tree");
        });
        
        this.showTempFiles = ko.pureComputed(() => {
            return this.node() === this.root;
        })
    }

    private processData() {
        const data = this.rawData;
        this.root = this.mapReport(data);
        this.sortBySize(this.root);
    }

    private sortBySize(node: serverStorageReportItem) {  //TODO: this can be done via hierarchy
        if (node.internalChildren && node.internalChildren.length) {
            node.internalChildren.forEach(x => this.sortBySize(x));

            node.internalChildren.sort((a, b) => descending(a.size, b.size));
        }
    }

    private mapReport(reportItem: detailedSystemStorageReportItemDto): serverStorageReportItem {
        const dataFile = this.mapDataFile(reportItem.Report);
        const journals = this.mapJournals(reportItem.Report);
        const tempFiles = this.mapTempFiles(reportItem.Report);

        return new serverStorageReportItem(reportItem.Environment,
            reportItem.Type.toLowerCase(),
            true,
            dataFile.size + journals.size + tempFiles.size,
            [dataFile, journals, tempFiles]);
    }

    private mapDataFile(report: Voron.Debugging.DetailedStorageReport): serverStorageReportItem {
        const dataFile = report.DataFile;

        const d = new serverStorageReportItem("Datafile", "data", false, dataFile.AllocatedSpaceInBytes);
        const tables = this.mapTables(report.Tables);
        const trees = this.mapTrees(report.Trees, "Trees");
        const freeSpace = new serverStorageReportItem("Free", "free", false, report.DataFile.FreeSpaceInBytes, []);
        const preallocatedBuffers = this.mapPreAllocatedBuffers(report.PreAllocatedBuffers);

        d.internalChildren = [tables, trees, freeSpace, preallocatedBuffers];
        
        return d;
    }

    private mapPreAllocatedBuffers(buffersReport: Voron.Debugging.PreAllocatedBuffersReport): serverStorageReportItem {
        const allocationTree = this.mapTree(buffersReport.AllocationTree);
        const buffersSpace = new serverStorageReportItem("Pre Allocated Buffers Space", "reserved", false, buffersReport.PreAllocatedBuffersSpaceInBytes);
        buffersSpace.pageCount = buffersReport.NumberOfPreAllocatedPages;

        const preAllocatedBuffers = new serverStorageReportItem("Pre Allocated Buffers", "reserved", false, buffersReport.AllocatedSpaceInBytes, [allocationTree, buffersSpace]);
        preAllocatedBuffers.customSizeProvider = (header: boolean) => {
            const allocatedSizeFormatted = generalUtils.formatBytesToSize(buffersReport.AllocatedSpaceInBytes);
            if (header) {
                return allocatedSizeFormatted;
            }
            const originalSizeFormatted = generalUtils.formatBytesToSize(buffersReport.OriginallyAllocatedSpaceInBytes);
            return `<span title="${allocatedSizeFormatted} available out of ${originalSizeFormatted} reserved">${allocatedSizeFormatted} (out of ${originalSizeFormatted})</span>`;
        };
        return preAllocatedBuffers;
    }

    private mapTables(tables: Voron.Data.Tables.TableReport[]): serverStorageReportItem {
        const mappedTables = tables.map(x => this.mapTable(x));

        return new serverStorageReportItem("Tables", "tables", false, mappedTables.reduce((p, c) => p + c.size, 0), mappedTables);
    }

    private mapTable(table: Voron.Data.Tables.TableReport): serverStorageReportItem {
        const structure = this.mapTrees(table.Structure, "Structure");

        const data = new serverStorageReportItem("Table Data", "table_data", false, table.DataSizeInBytes, []);
        const indexes = this.mapTrees(table.Indexes, "Indexes");

        const preallocatedBuffers = this.mapPreAllocatedBuffers(table.PreAllocatedBuffers);

        const totalSize = table.AllocatedSpaceInBytes;

        const tableItem = new serverStorageReportItem(table.Name, "table", true, totalSize, [
            structure,
            data,
            indexes,
            preallocatedBuffers
        ]);

        tableItem.numberOfEntries = table.NumberOfEntries;

        return tableItem;
    }

    private mapTrees(trees: Voron.Debugging.TreeReport[], name: string): serverStorageReportItem {
        return new serverStorageReportItem(name, name.toLowerCase(), false, trees.reduce((p, c) => p + c.AllocatedSpaceInBytes, 0), trees.map(x => this.mapTree(x)));
    }

    private mapTree(tree: Voron.Debugging.TreeReport): serverStorageReportItem {
        const children = (tree.Streams && tree.Streams.Streams) ? tree.Streams.Streams.map(x => this.mapStream(x)) : [];
        const item = new serverStorageReportItem(tree.Name, "tree", true, tree.AllocatedSpaceInBytes, children);
        item.pageCount = tree.PageCount;
        item.numberOfEntries = tree.NumberOfEntries;
        return item;
    }

    private mapStream(stream: Voron.Debugging.StreamDetails): serverStorageReportItem {
        const item = new serverStorageReportItem(stream.Name, "stream", false, stream.AllocatedSpaceInBytes, []);

        item.customSizeProvider = (header: boolean) => {
            const allocatedSizeFormatted = generalUtils.formatBytesToSize(stream.AllocatedSpaceInBytes);
            if (header) {
                return allocatedSizeFormatted;
            }
            const length = generalUtils.formatBytesToSize(stream.Length);
            return `<span title="stream length: ${length} / total allocation: ${allocatedSizeFormatted}">${length} / ${allocatedSizeFormatted}</span>`;
        }

        return item;
    }

    private mapJournals(report: Voron.Debugging.DetailedStorageReport): serverStorageReportItem {
        const journals = report.Journals.Journals;

        const mappedJournals = journals.map(journal => 
            new serverStorageReportItem(
                "Journal #" + journal.Number,
                "journal",
                false,
                journal.AllocatedSpaceInBytes,
                []
            ));

        return new serverStorageReportItem("Journals", "journals", false, mappedJournals.reduce((p, c) => p + c.size, 0), mappedJournals);
    }
    
    private mapTempFiles(report: Voron.Debugging.DetailedStorageReport): serverStorageReportItem {
        const tempFiles = report.TempBuffers;

        const mappedTemps = tempFiles.map(temp => {
            const item = new serverStorageReportItem(
                temp.Name,
                "temp",
                false,
                temp.AllocatedSpaceInBytes,
                []
            );
            
            item.recyclableJournal = temp.Type === "RecyclableJournal";
            
            return item;
        });

        return new serverStorageReportItem("Temporary Files", "tempFiles", false, mappedTemps.reduce((p, c) => p + c.size, 0), mappedTemps);
    }

    private initGraph() {
        this.detectContainerSize();
        
        this.svg = select("#storage-report-container .chart")
            .append("svg:svg")
            .attr("width", this.w)
            .attr("height", this.h)
            .attr("transform", "translate(.5,.5)");

        this.addHashing();
    }

    private detectContainerSize() {
        const $chartNode = $("#storage-report-container .chart");
        this.w = $chartNode.width();
        this.h = $chartNode.height();
    }

    private addHashing() { //TODO: do we need it?
        const defs = this.svg.append('defs');
        const g = defs.append("pattern")
            .attr('id', 'hash')
            .attr('patternUnits', 'userSpaceOnUse')
            .attr('width', '10')
            .attr('height', '10')
            .append("g").style("fill", "none")
            .style("stroke", "grey")
            .style("stroke-width", 1);
        g.append("path").attr("d", "M0,0 l10,10");
        g.append("path").attr("d", "M10,0 l-10,10");
    }

    private draw(goingIn: boolean) {
        const levelDown = goingIn === true;
        const levelUp = goingIn === false;
        
        const node = this.node() ?? this.root;
        
        //TODO: do we need to recompute hierarchy here?
        const tree = hierarchy<serverStorageReportItem>(this.root, d => d.internalChildren)
            .eachBefore(d => {
                // we don't use sum here, as the values are already summed-up - instead update readonly property 'value'
                (d.value as number) = d.data.size;
            });
        
        const layoutTree = treemap<serverStorageReportItem>()
            .size([this.w, this.h]);
        
        const treeToLayout = tree.find(x => x.data === node);
        this.currentPath(treeToLayout.ancestors().map(x => x.data));
        
        const currentRoot = layoutTree(treeToLayout.copy());
        
        if (!this.node()) {
            this.node(treeToLayout.data);
        }
        
        this.tooltip = select(".chart-tooltip");

        const oldNode = this.node();
        const oldLocation: positionAndSizes = { //TODO: store aside
            dx: 50,
            dy: 20,
            x :50, 
            y: 20
            // TODO: dx: oldNode.x1 - oldNode.x0,
            // dy: oldNode.y1 - oldNode.y0,
            // x: oldNode.x0,
            // y: oldNode.y0,
        };

        const nodes = currentRoot.children; //TODO: .filter(n => !n.children);

        if (levelDown) {
            this.animateZoomIn(nodes, oldLocation);
        } else if (levelUp) {
            this.animateZoomOut(nodes);
        } else {
            // initial state
            this.svg.select(".treemap")
                .remove();
            const container = this.svg.append("g")
                .classed("treemap", true);
            this.drawNewTreeMap(nodes, container);
        }
    }

    private animateZoomIn(nodes: HierarchyRectangularNode<serverStorageReportItem>[], oldLocation: positionAndSizes) {
        this.transitioning = true;

        const oldContainer = this.svg.select(".treemap");

        const newGroup = this.svg.append("g")
            .classed("treemap", true);

        const scaleX = this.w / oldLocation.dx;
        const scaleY = this.h / oldLocation.dy;
        const transX = -oldLocation.x * scaleX;
        const transY = -oldLocation.y * scaleY;

        oldContainer
            .selectAll("text")
            .transition()
            .duration(storageReport.animationLength / 4)
            .style('opacity', 0);

        oldContainer
            .transition()
            .duration(storageReport.animationLength)
            .attr("transform", "translate(" + transX + "," + transY + ")scale(" + scaleX + "," + scaleY + ")")
            .on("end", () => {
                const newCells = this.drawNewTreeMap(nodes, newGroup);
                newCells
                    .style('opacity', 0)
                    .transition()
                    .duration(storageReport.animationLength)
                    .style('opacity', 1)
                    .on("end", () => {
                        oldContainer.remove();
                        this.transitioning = false;
                    });
            });
    }

    private animateZoomOut(nodes: HierarchyRectangularNode<serverStorageReportItem>[]) {
        this.transitioning = true;

        const oldContainer = this.svg.select(".treemap");

        const newGroup = this.svg.append("g")
            .classed("treemap", true);

        const newCells = this.drawNewTreeMap(nodes, newGroup);

        newCells
            .style('opacity', 0)
            .transition()
            .duration(storageReport.animationLength)
            .style('opacity', 1)
            .on("end", () => {
                oldContainer.remove();
                this.transitioning = false;
            });
    }

    private drawNewTreeMap(nodes: HierarchyRectangularNode<serverStorageReportItem>[], container: Selection<any, any, HTMLElement, any>) {
        // eslint-disable-next-line @typescript-eslint/no-this-alias
        const self = this;
        const showTypeOffset = 7;
        const showTypePredicate = (d: HierarchyRectangularNode<serverStorageReportItem>) => d.data.showType && (d.y1 - d.y0) > 22 && (d.x1 - d.x0) > 20;

        const cell = container.selectAll("g.cell-no-such") // we always select non-existing nodes to draw from scratch - we don't update elements here
            .data(nodes)
            .enter()
            .append("svg:g")
            .attr("class", d => "cell " + d.data.type)
            .attr("transform", d => "translate(" + d.x0 + "," + d.y0 + ")")
            .on("click", (event: PointerEvent, d) => this.onClick(event, d.data, true))
            .on("mouseover", (event, data) => this.onMouseOver(event, data.data))
            .on("mouseout", () => this.onMouseOut())
            .on("mousemove", (e) => this.onMouseMove(e));

        const rectangles = cell.append("svg:rect")
            .attr("width", d => Math.max(0, (d.x1 - d.x0) - 1))
            .attr("height", d => Math.max(0, (d.y1 - d.y0) - 1));

        rectangles
            .filter(x => x.data.hasChildren())
            .style('cursor', 'pointer');

        
        cell.append("svg:text")
            .filter(d => (d.x1 - d.x0) > 20 && (d.y1 - d.y0) > 8)
            .attr("x", d => (d.x1 - d.x0) / 2)
            .attr("y", d => showTypePredicate(d) ? (d.y1 - d.y0) / 2 - showTypeOffset : (d.y1 - d.y0) / 2)
            .attr("dy", ".35em")
            .attr("text-anchor", "middle")
            .text(d => d.data.name)
            .each(function (d) {
                self.wrap(this, (d.x1 - d.x0));
            });

        cell.filter(d => showTypePredicate(d))
            .append("svg:text")
            .attr("x", d => (d.x1 - d.x0) / 2)
            .attr("y", d => showTypePredicate(d) ? (d.y1 - d.y0) / 2 + showTypeOffset : (d.y1 - d.y0) / 2)
            .attr("dy", ".35em")
            .attr("text-anchor", "middle")
            .text(d => _.upperFirst(d.data.type))
            .each(function (d) {
                self.wrap(this, (d.x1 - d.x0));
            });

        return cell;
    }

    wrap($self: any, width: number) {
        const self = select($self);
        let textLength = (self.node() as any).getComputedTextLength();
        let text = self.text();
        while (textLength > (width - 6) && text.length > 0) {
            text = text.slice(0, -1);
            self.text(text + '...');
            textLength = (self.node() as any).getComputedTextLength();
        }
    } 

    onClick(event: PointerEvent, d: serverStorageReportItem, goingIn: boolean) {
        if (this.transitioning || this.node() === d) {
            return;
        }

        this.node(d);
        this.draw(goingIn);

        this.updateTooltips();
        
        if (event) {
            event.stopPropagation();
        }
    }
    
    private updateTooltips() {
        $('#storage-report [data-toggle="tooltip"]').tooltip();
    }

    private onMouseMove(e: any) {
        // eslint-disable-next-line prefer-const
        let [x, y] = pointer(e, this.svg.node());

        const tooltipWidth = $(".chart-tooltip").width() + 20;

        x = Math.min(x, Math.max(this.w - tooltipWidth, 0));

        this.tooltip
            .style("left", (x + 10) + "px")
            .style("top", (y + 10) + "px");
    }

    private onMouseOver(event: any, d: serverStorageReportItem) {
        this.tooltip.transition()
            .duration(200)
            .style("opacity", 1);
        let html = "<span class='name'>Name: " + d.name + "</span>";
        if (d.showType) {
            html += "<span>Type: <strong>" + _.upperFirst(d.type) + "</strong></span>";
        }
        if (this.shouldDisplayNumberOfEntries(d)) {
            html += "<span>Entries: <strong>" + d.numberOfEntries.toLocaleString() + "</strong></span>";
        }
        html += "<span class='size'>Size: <strong>" + generalUtils.formatBytesToSize(d.size) + "</strong></span>";

        this.tooltip.html(html);
        this.onMouseMove(event);
    }

    private shouldDisplayNumberOfEntries(d: serverStorageReportItem) {
        return d.type === "tree" || d.type === "table";
    }

    private onMouseOut() {
        this.tooltip.transition()
            .duration(500)
            .style("opacity", 0);
    }
}
