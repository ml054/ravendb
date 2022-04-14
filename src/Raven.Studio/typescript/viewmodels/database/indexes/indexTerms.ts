import viewModelBase from "viewmodels/viewModelBase";
import getIndexTermsCommand from "commands/database/index/getIndexTermsCommand";
import getIndexEntriesFieldsCommand from "commands/database/index/getIndexEntriesFieldsCommand";
import queryCriteria from "models/database/query/queryCriteria";
import recentQueriesStorage from "common/storage/savedQueriesStorage";
import queryUtil from "common/queryUtil";
import appUrl from "common/appUrl";
import eventsCollector from "common/eventsCollector";
import showDataDialog from "viewmodels/common/showDataDialog";
import copyToClipboard from "common/copyToClipboard";
import app from "durandal/app";
import generalUtils from "common/generalUtils";
import recentError from "common/notifications/models/recentError";

type termsForField = {
    name: string;
    terms: KnockoutObservableArray<string>;
    fromValue: string;
    type: fieldType;
    hasMoreTerms: KnockoutObservable<boolean>;
    loadInProgress: KnockoutObservable<boolean>;
    loadError: KnockoutObservable<string>;
}

type fieldType = "static" | "dynamic";

class indexTerms extends viewModelBase {
    
    view = require("views/database/indexes/indexTerms.html");

    fields = ko.observableArray<termsForField>();
    indexName: string;

    indexPageUrl: KnockoutComputed<string>;

    static readonly termsPageLimit = 500; 

    constructor() {
        super();

        this.bindToCurrentInstance("navigateToQuery");
    }

    activate(indexName: string): JQueryPromise<getIndexEntriesFieldsCommandResult> {
        super.activate(indexName);

        this.indexName = indexName;
        this.indexPageUrl = this.appUrls.editIndex(this.indexName);
        return this.fetchIndexEntriesFields(indexName);
    }

    fetchIndexEntriesFields(indexName: string) {
        return new getIndexEntriesFieldsCommand(indexName, this.activeDatabase())
            .execute()
            .done((fields) => this.processFields(fields));
    }

    navigateToQuery(fieldName: string, term: string) {
        const query = queryCriteria.empty();
        const queryText = queryUtil.formatIndexQuery(this.indexName, fieldName, term);
        
        query.queryText(queryText);
        query.name("Index terms for " + this.indexName + " (" + fieldName + ": " + term + ")");
        query.recentQuery(true);

        const queryDto = query.toStorageDto();
        recentQueriesStorage.saveAndNavigate(this.activeDatabase(), queryDto);
    }

    static createTermsForField(fieldName: string, type: fieldType): termsForField {
        return {
            fromValue: null,
            name: fieldName,
            hasMoreTerms: ko.observable<boolean>(true),
            terms: ko.observableArray<string>(),
            type: type,
            loadInProgress: ko.observable<boolean>(false),
            loadError: ko.observable<string>()
        }
    }

    private processFields(fields: getIndexEntriesFieldsCommandResult) {
        const staticFields = fields.Static.map(fieldName => indexTerms.createTermsForField(fieldName, "static"));
        const dynamicFields = fields.Dynamic.map(fieldName => indexTerms.createTermsForField(fieldName, "dynamic"));
        
        this.fields(staticFields.concat(dynamicFields));

        this.fields()
            .forEach(field => this.loadTerms(this.indexName, field));
    }
    
    private loadTerms(indexName: string, termsForField: termsForField): JQueryPromise<Raven.Client.Documents.Queries.TermsQueryResult> {  // fetch one more to find out if we have more
        return new getIndexTermsCommand(indexName, null, termsForField.name, this.activeDatabase(), indexTerms.termsPageLimit + 1, termsForField.fromValue)  
            .execute()
            .done((loadedTermsResponse: Raven.Client.Documents.Queries.TermsQueryResult) => {
                let loadedTerms = loadedTermsResponse.Terms;
                if (loadedTerms.length > indexTerms.termsPageLimit) {
                    termsForField.hasMoreTerms(true);
                    loadedTerms = loadedTerms.slice(0, indexTerms.termsPageLimit);
                } else {
                    termsForField.hasMoreTerms(false);
                }
                termsForField.terms.push(...loadedTerms);
                if (loadedTerms.length > 0) {
                    termsForField.fromValue = loadedTerms[loadedTerms.length - 1];
                }
            })
            .fail((response: JQueryXHR) => {
                termsForField.hasMoreTerms(false);

                const messageAndOptionalException = recentError.tryExtractMessageAndException(response.responseText);
                termsForField.loadError(generalUtils.trimMessage(messageAndOptionalException.message));
            });
    }
    
    showData(term: string) {
        app.showBootstrapDialog(new showDataDialog("Index Term Value", term, "plain"));
    }

    copyTerm(term: string) {
        copyToClipboard.copy(term, "Index term was copied to clipboard.");
    }
    
    loadMore(fieldName: string) {
        eventsCollector.default.reportEvent("terms", "load-more");
        
        const field = this.fields().find(x => x.name === fieldName);

        if (!field || !field.hasMoreTerms()) {
            return;
        }
        field.loadInProgress(true);

        this.loadTerms(this.indexName, field)
            .always(() => field.loadInProgress(false));
    }
}

export = indexTerms;
