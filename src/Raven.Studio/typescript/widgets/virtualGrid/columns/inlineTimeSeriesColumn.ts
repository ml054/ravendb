﻿/// <reference path="../../../../typings/tsd.d.ts"/>
import timeSeriesColumn from "widgets/virtualGrid/columns/timeSeriesColumn";
import virtualGridController from "widgets/virtualGrid/virtualGridController";
import document from "models/database/documents/document";

/**
 * Virtual grid column that renders inline time series - see RavenDB-14431
 */
class inlineTimeSeriesColumn extends timeSeriesColumn<document> {
    
    static readonly inlineColumnName = "Time Series";
    
    constructor(gridController: virtualGridController<any>, width: string, opts: timeSeriesColumnOpts<any> = {}) {
        super(gridController, null, inlineTimeSeriesColumn.inlineColumnName, width, opts);
    }

    getName() {
        return inlineTimeSeriesColumn.inlineColumnName;
    }
    
    getCellValue(item: document): any {
        return item.toDto(true);
    }
}

export = inlineTimeSeriesColumn;
