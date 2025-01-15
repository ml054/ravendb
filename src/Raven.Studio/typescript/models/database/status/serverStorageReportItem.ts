/// <reference path="../../../../typings/tsd.d.ts"/>

import generalUtils = require("common/generalUtils");

class serverStorageReportItem {

    name: string;
    type: string;
    internalChildren: serverStorageReportItem[];
    size?: number;
    length?: number;
    pageCount: number = null;
    showType: boolean;
    w?: number; // used for storing text width
    numberOfEntries: number = null;
    customSizeProvider: (header: boolean) => string;
    isGrouped: boolean;
    
    recyclableJournal = false;

    constructor(name: string, type: string, showType: boolean, size: number, internalChildren: serverStorageReportItem[] = null, isGrouped = false) {
        this.name = name;
        this.type = type;
        this.showType = showType;
        this.size = size;
        this.internalChildren = internalChildren;
        this.isGrouped = isGrouped;
    }

    formatSize(header: boolean) {
        return this.customSizeProvider ? this.customSizeProvider(header) : generalUtils.formatBytesToSize(this.size);
    }

    formatPercentage(parentSize: number) {
        return (this.size * 100 / parentSize).toFixed(2) + '%';
    }

    hasChildren(): boolean {
        return this.internalChildren && this.internalChildren.length > 0;
    }
}

export = serverStorageReportItem;
