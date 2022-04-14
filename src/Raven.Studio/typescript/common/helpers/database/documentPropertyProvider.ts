/// <reference path="../../../../typings/tsd.d.ts" />

import document from "models/database/documents/document";
import database from "models/resources/database";
import getDocumentsPreviewCommand from "commands/database/documents/getDocumentsPreviewCommand";
import getDocumentWithMetadataCommand from "commands/database/documents/getDocumentWithMetadataCommand";
import messagePublisher from "common/messagePublisher";
import textColumn from "widgets/virtualGrid/columns/textColumn";
import customColumn from "widgets/virtualGrid/columns/customColumn";

class documentPropertyProvider {
    private fullDocumentsCache = new Map<string, document>();

    private readonly db: database;

    constructor(db: database) {
        this.db = db;
    }

    resolvePropertyValue(doc: document, column: textColumn<document>, onValue: (v: any) => void, onEvalError?: (e: Error) => void): void {
        if (this.needsToFetchValue(doc, column)) {
            this.fetchAndRespond(doc, column, onValue);
        } else {
            try {
                onValue(column.getCellValue(doc));
            } catch (e) {
                if (onEvalError) {
                    onEvalError(e);
                } else {
                    throw e;
                }
            }
        }
    }

    private needsToFetchValue(doc: document, column: textColumn<document>): boolean {
        const valueAccessor = column.valueAccessor;
        if (_.isFunction(valueAccessor)) {
            if (column instanceof customColumn) {
                return !_.every(column.tryGuessRequiredProperties(), p => this.hasEntireValue(doc, p));
            } else {
                return false;
            }
        } else {
            return !this.hasEntireValue(doc, valueAccessor);
        }
    }

    private fetchAndRespond(doc: document, column: textColumn<document>, onValue: (v: any) => void, onEvalError?: (e: Error) => void) {
        if (this.fullDocumentsCache.has(doc.getId())) {
            const cachedValue = this.fullDocumentsCache.get(doc.getId());
            onValue(column.getCellValue(cachedValue));
            return;
        }

        new getDocumentWithMetadataCommand(doc.getId(), this.db, true)
            .execute()
            .done((fullDocument: document) => {
                try {
                    onValue(column.getCellValue(fullDocument));
                } catch (e) {
                    if (onEvalError) {
                        onEvalError(e);
                    } else {
                        throw e;
                    }
                }
            })
            .fail((response: JQueryXHR) => {
                messagePublisher.reportError("Failed to fetch document: " + doc.getId(), response.responseText);
            });
    }

    private hasEntireValue(doc: document, property: string) {
        const meta = doc.__metadata as any;
        const arrays = meta[getDocumentsPreviewCommand.ArrayStubsKey];
        if (arrays && property in arrays) {
            return false;
        }

        const objects = meta[getDocumentsPreviewCommand.ObjectStubsKey];
        if (objects && property in objects) {
            return false;
        }

        const trimmed = meta[getDocumentsPreviewCommand.TrimmedValueKey];
        if (trimmed && _.includes(trimmed, property)) {
            return false;
        }
        return true;
    }
    
}

export = documentPropertyProvider;
