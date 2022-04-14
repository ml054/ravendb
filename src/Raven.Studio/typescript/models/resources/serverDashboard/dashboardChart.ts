/// <reference path="../../../../typings/tsd.d.ts"/>

import d3 from 'd3';

type chartItemData = {
    x: Date,
    y: number
}

type chartData = {
    id: string,
    values: chartItemData[],
}

type chartOpts = {
    yMaxProvider?: () => number | null;
    useSeparateYScales?: boolean;
    topPaddingProvider?: (key: string) => number;
    tooltipProvider?: (data: dashboardChartTooltipProviderArgs) => string;
}

class dashboardChart {
    
    static readonly defaultTopPadding = 5;
    
    private width: number;
    private height: number;
    
    private minDate: Date = null;
    private maxDate: Date = null;
    private data: chartData[] = [];
    private opts: chartOpts;
    
    private svg: d3.Selection<void>;
    private pointer: d3.Selection<void>;
    private lastXPosition: number = null;
    private tooltip: d3.Selection<void>;
    
    private xScale: d3.time.Scale<number, number>;
    
    private readonly containerSelector: string;
    
    constructor(containerSelector: string, opts?: chartOpts) {
        this.opts = opts || {} as any;
        this.containerSelector = containerSelector;
        
        if (!this.opts.topPaddingProvider) {
            this.opts.topPaddingProvider = () => dashboardChart.defaultTopPadding;
        }
        
        const container = d3.select(containerSelector);
        
        const $container = $(containerSelector);
        
        this.width = $container.innerWidth();
        this.height = $container.innerHeight() - 1;

        this.svg = container
            .append("svg")
            .attr("width", this.width)
            .attr("height", this.height);
        
        const gridContainer = this.svg
            .append("g")
            .attr("transform", "translate(-0.5, 0)")
            .attr("class", "grid");
        
        this.svg
            .append("g")
            .attr("class", "series");
        
        this.drawGrid(gridContainer);
        
        const pointer = this.svg
            .append("g")
            .attr("class", "pointer");
        
        this.pointer = pointer.append("line")
            .attr("class", "pointer-line")
            .attr("x1", 0)
            .attr("x2", 0)
            .attr("y1", 0)
            .attr("y2", this.height)
            .style("stroke-opacity", 0);
        
        this.tooltip = d3.select(".tooltip");
        
        if (this.opts.tooltipProvider) {
            this.setupValuesPreview();
        }
    }
    
    private drawGrid(gridContainer: d3.Selection<any>) {
        const gridLocation = _.range(0, this.width, 20)
            .map(x => this.width - x);
        
        const lines = gridContainer.selectAll("line")
            .data(gridLocation);
        
        lines
            .exit()
            .remove();
        
        lines
            .enter()
            .append("line")
            .attr("class", "grid-line")
            .attr("x1", x => x)
            .attr("x2", x => x)
            .attr("y1", 0)
            .attr("y2", this.height);
    }
    
    onResize() {
        const container = d3.select(this.containerSelector);
        
        const $container = $(this.containerSelector);
        
        this.width = $container.innerWidth();
        this.height = $container.innerHeight() - 1;

        this.svg = container
            .select("svg")
            .attr("width", this.width)
            .attr("height", this.height);
        
        const gridContainer = this.svg.select(".grid");
        gridContainer.selectAll("line").remove();
        
        this.drawGrid(gridContainer);
    }
    
    private setupValuesPreview() {
        this.svg
            .on("mousemove.tip", () => {
                const node = this.svg.node();
                const mouseLocation = d3.mouse(node);
                this.pointer
                    .attr("x1", mouseLocation[0] + 0.5)
                    .attr("x2", mouseLocation[0] + 0.5);
                
                this.updateTooltip();
            })
            .on("mouseenter.tip", () => {
                this.pointer
                    .transition()
                    .duration(200)
                    .style("stroke-opacity", 1);
                
                this.showTooltip();
            })
            .on("mouseleave.tip", () => {
                this.pointer
                    .transition()
                    .duration(100)
                    .style("stroke-opacity", 0);
                
                this.hideTooltip();
            });
    }
    
    showTooltip() {
        this.tooltip
            .style('display', undefined)
            .transition()
            .duration(250)
            .style("opacity", 1);
    }
    
    updateTooltip(passive = false) {
        let xToUse = null as number;
        if (passive) {
            // just update contents
            xToUse = this.lastXPosition;
        } else {
            xToUse = d3.mouse(this.svg.node())[0];
            this.lastXPosition = xToUse;

            const globalLocation = d3.mouse(d3.select("#dashboard-container").node());
            const [x, y] = globalLocation;
            this.tooltip
                .style("left", (x + 10) + "px")
                .style("top", (y + 10) + "px")
                .style('display', undefined);
        }
        
        if (!_.isNull(xToUse) && this.minDate) {
            const data = this.findClosestData(xToUse);
            const html = this.opts.tooltipProvider(data) || "";
            
            if (html) {
                this.tooltip.html(html);
                this.tooltip.style("display", undefined);
            } else {
                this.tooltip.style("display", "none");
            }
            
        }
    }
    
    private findClosestData(xToUse: number): dashboardChartTooltipProviderArgs {
        const hoverTime = this.xScale.invert(xToUse);

        if (hoverTime.getTime() < this.minDate.getTime()) {
            return null;
        } else {
            
            const hoverTicks = hoverTime.getTime();
            let bestIndex = 0;
            let bestDistance = 99999;
            
            for (let i = 0; i < this.data[0].values.length; i++) {
                const dx = Math.abs(hoverTicks - this.data[0].values[i].x.getTime());
                if (dx < bestDistance) {
                    bestDistance = dx;
                    bestIndex = i;
                }
            }
            
            const values = {} as dictionary<number>; 
                this.data.forEach(d => {
                values[d.id] = d.values[bestIndex].y;
            });
            
            return {
                date: this.data[0].values[bestIndex].x,
                values: values
            };
        }
    }
    
    hideTooltip() {
        this.tooltip.transition()
            .duration(250)
            .style("opacity", 0)
            .each('end', () => this.tooltip.style('display', 'none'));

        this.lastXPosition = null;
    }
    
    onData(time: Date, data: { key: string,  value: number }[] ) {
        if (!this.minDate) {
            this.minDate = time;
        }
        this.maxDate = time;
        
        data.forEach(dataItem => {
            let dataEntry = this.data.find(x => x.id === dataItem.key);
            
            if (!dataEntry) {
                dataEntry = {
                    id: dataItem.key,
                    values: []
                };
                this.data.push(dataEntry);
            }

            dataEntry.values.push({
                x: time,
                y: dataItem.value
            });
        });
        
        this.maybeTrimData();
       
        this.draw();
        
        this.updateTooltip(true);
    }
    
    private maybeTrimData() {
        let hasAnyTrim = false;
        for (let i = 0; i < this.data.length; i++) {
            const entry = this.data[i];
            
            if (entry.values.length > 2000) {
                entry.values = entry.values.splice(1500);
                hasAnyTrim = true;
            }
        }
        
        if (hasAnyTrim) {
            this.minDate = _.min(this.data.map(x => x.values[0].x));
        }
    }
    
    private createLineFunctions(): Map<string, d3.svg.Line<chartItemData>> {
        const timePerPixel = 500;
        const maxTime = this.maxDate;
        const minTime = new Date(maxTime.getTime() - this.width * timePerPixel);

        const result = new Map<string, d3.svg.Line<chartItemData>>();

        this.xScale = d3.time.scale()
            .range([0, this.width])
            .domain([minTime, maxTime]);
        
        const yScaleCreator = (maxValue: number, topPadding: number) => {
            if (!maxValue) {
                maxValue = 1;
            }
            return d3.scale.linear()
                .range([topPadding != null ? topPadding : dashboardChart.defaultTopPadding, this.height])
                .domain([maxValue, 0]);
        };
        
        if (this.opts.yMaxProvider != null) {
            const yScale = yScaleCreator(this.opts.yMaxProvider(), this.opts.topPaddingProvider(null));

            const lineFunction = d3.svg.line<chartItemData>()
                .x(x => this.xScale(x.x))
                .y(x => yScale(x.y));
            
            this.data.forEach(data => {
                result.set(data.id, lineFunction);
            });
        } else if (this.opts.useSeparateYScales) {
            this.data.forEach(data => {
                const yMax = d3.max(data.values.map(x => x.y));
                const yScale = yScaleCreator(yMax, this.opts.topPaddingProvider(data.id));

                const lineFunction = d3.svg.line<chartItemData>()
                    .x(x => this.xScale(x.x))
                    .y(x => yScale(x.y));
                
                result.set(data.id, lineFunction);
            });
        } else {
            const yMax = d3.max(this.data.map(d => d3.max(d.values.map(x => x.y))));
            const yScale = yScaleCreator(yMax, this.opts.topPaddingProvider(null));

            const lineFunction = d3.svg.line<chartItemData>()
                .x(x => this.xScale(x.x))
                .y(x => yScale(x.y));

            this.data.forEach(data => {
                result.set(data.id, lineFunction);
            });
        }
     
        return result;
    }
    
    draw() {
        const series = this.svg
            .select(".series")
            .selectAll(".serie")
            .data(this.data, x => x.id);
        
        series
            .exit()
            .remove();
        
        const enteringSerie = series
            .enter()
            .append("g")
            .attr("class", x => "serie " + x.id);
        
        const lineFunctions = this.createLineFunctions();
        
        enteringSerie
            .append("path")
            .attr("class", "line")
            .attr("d", d => lineFunctions.get(d.id)(d.values));

        enteringSerie
            .append("path")
            .attr("class", "fill")
            .attr("d", d => lineFunctions.get(d.id)(dashboardChart.closedPath(d.values)));
        
        series
            .select(".line")
            .attr("d", d => lineFunctions.get(d.id)(d.values));

        series
            .select(".fill")
            .attr("d", d => lineFunctions.get(d.id)(dashboardChart.closedPath(d.values)));
    }
    
    private static closedPath(input: chartItemData[]): chartItemData[] {
        if (input.length === 0) {
            return input;
        }
        
        const firstElement: chartItemData = {
            x: input[0].x,
            y: 0
        };
        
        const lastElement: chartItemData = {
            x: _.last(input).x,
            y: 0
        };
        
        return [firstElement].concat(input, [lastElement]);
    } 
}

export = dashboardChart;
