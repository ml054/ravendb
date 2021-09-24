/// <reference path="../../../../typings/tsd.d.ts"/>
import genUtils = require("common/generalUtils");
import queryUtil = require("common/queryUtil");
import moment = require("moment");

class queryCriteria {

    name = ko.observable<string>("");
    showFields = ko.observable<boolean>(false);
    indexEntries = ko.observable<boolean>(false);
    queryText = ko.observable<string>("");
    metadataOnly = ko.observable<boolean>(false);
    recentQuery = ko.observable<boolean>(false);
    graphOutput = ko.observable<boolean>(false);
    diagnostics = ko.observable<boolean>(false);
    
    validationGroup: KnockoutValidationGroup;

    static empty() {
        return new queryCriteria();
    }

    constructor() {
        this.initObservables();
        this.initValidation();
    }
    
    private initValidation() {
        this.queryText.extend({
            // We want to be able to send invalid queries in order to get the server side error as well.
            // aceValidation: true
        });
        
        this.validationGroup = ko.validatedObservable({
            queryText: this.queryText
        })
    }

    private initObservables() {
        this.showFields.subscribe(showFields => {
            if (showFields && this.indexEntries()) {
                this.indexEntries(false);
            }
        });

        this.indexEntries.subscribe(indexEntries => {
            if (indexEntries && this.showFields()) {
                this.showFields(false);
            }
        });
    }

    updateUsing(storedQuery: storedQueryDto) {
        this.queryText(storedQuery.queryText);
        this.recentQuery(storedQuery.recentQuery);
    }

    toStorageDto(): storedQueryDto {
        const name = this.name();
        const queryText = this.queryText();

        return {
            name: name,
            queryText: queryText,
            recentQuery: this.recentQuery(),
            modificationDate: moment().format("YYYY-MM-DD HH:mm"),
            hash: genUtils.hashCode(name + (queryText || "")) 
        };
    }

    setSelectedIndex(indexName: string) {
        let rql = "from ";
        if (indexName.startsWith(queryUtil.DynamicPrefix)) {
            rql += indexName.substring(queryUtil.DynamicPrefix.length);
        } else if (indexName === queryUtil.AllDocs) {
            rql += "@all_docs";
        } else {
            rql += "index '" + indexName + "'";
        }
        this.queryText(rql);
    }

    copyFrom(incoming: queryDto) {
        this.name("");
        this.queryText(incoming.queryText);
        this.recentQuery(incoming.recentQuery);
    }
    
    clone(): queryCriteria {
        const clonedItem = new queryCriteria();

        clonedItem.name(this.name());
        clonedItem.queryText(this.queryText());
        clonedItem.showFields(this.showFields());
        clonedItem.indexEntries(this.indexEntries());
        clonedItem.metadataOnly(this.metadataOnly());
        clonedItem.graphOutput(this.graphOutput());
        clonedItem.recentQuery(this.recentQuery());
        clonedItem.diagnostics(this.diagnostics());
        
        return clonedItem;
    }
}

export = queryCriteria; 
