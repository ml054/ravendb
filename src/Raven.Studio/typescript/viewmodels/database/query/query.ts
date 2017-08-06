import app = require("durandal/app");
import appUrl = require("common/appUrl");
import viewModelBase = require("viewmodels/viewModelBase");
import getDatabaseStatsCommand = require("commands/resources/getDatabaseStatsCommand");
import aceEditorBindingHandler = require("common/bindingHelpers/aceEditorBindingHandler");
import messagePublisher = require("common/messagePublisher");
import getCollectionsStatsCommand = require("commands/database/documents/getCollectionsStatsCommand");
import collectionsStats = require("models/database/documents/collectionsStats"); 
import datePickerBindingHandler = require("common/bindingHelpers/datePickerBindingHandler");
import deleteDocumentsMatchingQueryConfirm = require("viewmodels/database/query/deleteDocumentsMatchingQueryConfirm");
import deleteDocsMatchingQueryCommand = require("commands/database/documents/deleteDocsMatchingQueryCommand");
import notificationCenter = require("common/notifications/notificationCenter");

import queryCommand = require("commands/database/query/queryCommand");
import database = require("models/resources/database");
import querySort = require("models/database/query/querySort");
import collection = require("models/database/documents/collection");
import getTransformersCommand = require("commands/database/transformers/getTransformersCommand");
import document = require("models/database/documents/document");
import queryStatsDialog = require("viewmodels/database/query/queryStatsDialog");
import transformerType = require("models/database/index/transformer");
import recentQueriesStorage = require("common/storage/recentQueriesStorage");
import queryUtil = require("common/queryUtil");
import eventsCollector = require("common/eventsCollector");
import queryCriteria = require("models/database/query/queryCriteria");
import queryTransformerParameter = require("models/database/query/queryTransformerParameter");

import documentBasedColumnsProvider = require("widgets/virtualGrid/columns/providers/documentBasedColumnsProvider");
import virtualColumn = require("widgets/virtualGrid/columns/virtualColumn");
import virtualGridController = require("widgets/virtualGrid/virtualGridController");
import columnPreviewPlugin = require("widgets/virtualGrid/columnPreviewPlugin");
import textColumn = require("widgets/virtualGrid/columns/textColumn");
import columnsSelector = require("viewmodels/partial/columnsSelector");
import popoverUtils = require("common/popoverUtils");
import getCustomFunctionsCommand = require("commands/database/documents/getCustomFunctionsCommand");
import customFunctions = require("models/database/documents/customFunctions");
import evaluationContextHelper = require("common/helpers/evaluationContextHelper");


type indexItem = {
    name: string;
    isMapReduce: boolean;
}

type filterType = "in" | "string" | "range";

type stringSearchType = "Starts With" | "Ends With" | "Contains" | "Exact";

type rangeSearchType = "Numeric Double" | "Numeric Long" | "Alphabetical" | "Datetime";

type fetcherType = (skip: number, take: number) => JQueryPromise<pagedResult<document>>;

class query extends viewModelBase {

    static readonly ContainerSelector = "#queryContainer";
    static readonly $body = $("body");

    static readonly SearchTypes: stringSearchType[] = ["Exact", "Starts With", "Ends With", "Contains"];
    static readonly RangeSearchTypes: rangeSearchType[] = ["Numeric Double", "Numeric Long", "Alphabetical", "Datetime"];
    static readonly SortTypes: querySortType[] = ["Ascending", "Descending", "Range Ascending", "Range Descending"];

    private gridController = ko.observable<virtualGridController<any>>();

    recentQueries = ko.observableArray<storedQueryDto>();
    allTransformers = ko.observableArray<Raven.Client.Documents.Transformers.TransformerDefinition>();

    collections = ko.observableArray<collection>([]);
    indexes = ko.observableArray<indexItem>();
    indexFields = ko.observableArray<string>();
    querySummary = ko.observable<string>();

    criteria = ko.observable<queryCriteria>(queryCriteria.empty());
    cacheEnabled = ko.observable<boolean>(true);

    private indexEntrieStateWasTrue: boolean = false; // Used to save current query settings when switching to a 'dynamic' index

    columnsSelector = new columnsSelector<document>();

    uiTransformer = ko.observable<string>(); // represents UI value, which might not be yet applied to criteria 
    uiTransformerParameters = ko.observableArray<queryTransformerParameter>(); // represents UI value, which might not be yet applied to criteria 

    fetcher = ko.observable<fetcherType>();
    queryStats = ko.observable<Raven.Client.Documents.Queries.QueryResult<any>>();
    staleResult: KnockoutComputed<boolean>;
    dirtyResult = ko.observable<boolean>();

    canDeleteDocumentsMatchingQuery: KnockoutComputed<boolean>;
    isMapReduceIndex: KnockoutComputed<boolean>;
    isDynamicIndex: KnockoutComputed<boolean>;
    isAutoIndex: KnockoutComputed<boolean>;
    isStaticIndexSelected: KnockoutComputed<boolean>;

    private columnPreview = new columnPreviewPlugin<document>();

    initialSelectedIndex: string;
    selectedIndex: KnockoutComputed<string>;
    selectedIndexLabel: KnockoutComputed<string>;
    hasEditableIndex: KnockoutComputed<boolean>;

    editIndexUrl: KnockoutComputed<string>;
    indexPerformanceUrl: KnockoutComputed<string>;
    termsUrl: KnockoutComputed<string>;
    visualizerUrl: KnockoutComputed<string>;
    rawJsonUrl = ko.observable<string>();
    csvUrl = ko.observable<string>();

    isLoading = ko.observable<boolean>(false);
    containsAsterixQuery: KnockoutComputed<boolean>; // query contains: *.* ?

    private customFunctionsContext: object;

    /*TODO
    isTestIndex = ko.observable<boolean>(false);
    
    selectedResultIndices = ko.observableArray<number>();
    
    enableDeleteButton: KnockoutComputed<boolean>;
    warningText = ko.observable<string>();
    isWarning = ko.observable<boolean>(false);
    
    indexSuggestions = ko.observableArray<indexSuggestion>([]);
    showSuggestions: KnockoutComputed<boolean>;

    static queryGridSelector = "#queryResultsGrid";*/

    constructor() {
        super();

        aceEditorBindingHandler.install();
        datePickerBindingHandler.install();

        this.initObservables();
        this.initValidation();

        this.bindToCurrentInstance("runRecentQuery", "selectTransformer");
    }

    private initObservables() {
        this.selectedIndex = ko.pureComputed(() => {
            const stats = this.queryStats();
            if (!stats) {
                return this.initialSelectedIndex;
            }

            return stats.IndexName;
        });
        this.selectedIndexLabel = ko.pureComputed(() => {
            let indexName = this.selectedIndex();
            return (indexName === "AllDocs") ? "All Documents" : indexName;
        });

        this.hasEditableIndex = ko.pureComputed(() => {
            let indexName = this.selectedIndex();
            return indexName ? !indexName.startsWith(queryUtil.DynamicPrefix) : false;
        });

        this.editIndexUrl = ko.pureComputed(() => this.selectedIndex() ? appUrl.forEditIndex(this.selectedIndex(), this.activeDatabase()) : null);
        this.indexPerformanceUrl = ko.pureComputed(() => this.selectedIndex() ? appUrl.forIndexPerformance(this.activeDatabase(), this.selectedIndex()) : null);
        this.termsUrl = ko.pureComputed(() => this.selectedIndex() ? appUrl.forTerms(this.selectedIndex(), this.activeDatabase()) : null);
        this.visualizerUrl = ko.pureComputed(() => this.selectedIndex() ? appUrl.forVisualizer(this.activeDatabase(), this.selectedIndex()) : null);

        this.isMapReduceIndex = ko.pureComputed(() => {
            let indexName = this.selectedIndex();
            if (!indexName)
                return false;

            const indexes = this.indexes() || [];
            const currentIndex = indexes.find(i => i.name === indexName);
            return !!currentIndex && currentIndex.isMapReduce;
        });
        this.isAutoIndex = ko.pureComputed(() => {
            let indexName = this.selectedIndex();
            if (!indexName)
                return false;

            return indexName.startsWith(queryUtil.AutoPrefix);
        });

        this.isStaticIndexSelected = ko.pureComputed(() => {
            let indexName = this.selectedIndex();
            if (!indexName)
                return false;

            return !indexName.startsWith(queryUtil.DynamicPrefix);
        });

        this.isDynamicIndex = ko.pureComputed(() => {
            let indexName = this.selectedIndex();
            if (!indexName)
                return false;

            const indexes = this.indexes() || [];
            const currentIndex = indexes.find(i => i.name === indexName);
            return !currentIndex;
        });

        this.canDeleteDocumentsMatchingQuery = ko.pureComputed(() => {
            return !this.isMapReduceIndex() && !this.isDynamicIndex();
        });

        this.containsAsterixQuery = ko.pureComputed(() => this.criteria().queryText().includes("*.*"));

        const dateToString = (input: moment.Moment) => input ? input.format("YYYY-MM-DDTHH:mm:00.0000000") : "";

        this.staleResult = ko.pureComputed(() => {
            //TODO: return false for test index
            const stats = this.queryStats();
            return stats ? stats.IsStale : false;
        });

        this.cacheEnabled.subscribe(() => {
            eventsCollector.default.reportEvent("query", "toggle-cache");
        });

        this.isLoading.extend({ rateLimit: 100 });

        const criteria = this.criteria();

        criteria.showFields.subscribe(() => this.runQuery());   
      
        criteria.indexEntries.subscribe((checked) => {
            if (checked && this.isDynamicIndex()) {
                criteria.indexEntries(false);
            } else {
                // run index entries option only if not dynamic index
                this.runQuery();
            }
        });

         /* TODO
        this.showSuggestions = ko.computed<boolean>(() => {
            return this.indexSuggestions().length > 0;
        });

        this.selectedIndex.subscribe(index => this.onIndexChanged(index));

        this.enableDeleteButton = ko.computed(() => {
            var currentIndex = this.indexes().find(i => i.name === this.selectedIndex());
            var isMapReduce = this.isMapReduceIndex();
            var isDynamic = this.isDynamicIndex();
            return !!currentIndex && !isMapReduce && !isDynamic;
        });*/
    }

    private initValidation() {
    }

    canActivate(args: any) {
        super.canActivate(args);

        this.loadRecentQueries();

        const initTask = $.Deferred<canActivateResultDto>();

        this.fetchAllTransformers(this.activeDatabase())
            .done(() => initTask.resolve({ can: true }))
            .fail(() => initTask.resolve({ can: false }));

        return initTask;
    }

    activate(indexNameOrRecentQueryHash?: string) {
        super.activate(indexNameOrRecentQueryHash);

        this.updateHelpLink('KCIMJK');
        
        const db = this.activeDatabase();

        return $.when<any>(this.fetchAllCollections(db), this.fetchAllIndexes(db), this.fetchCustomFunctions(db))
            .done(() => this.selectInitialQuery(indexNameOrRecentQueryHash));
    }

    detached() {
        super.detached();
        aceEditorBindingHandler.detached();
    }

    attached() {
        super.attached();

        this.createKeyboardShortcut("ctrl+enter", () => this.runQuery(), query.ContainerSelector);

        /* TODO
        this.createKeyboardShortcut("F2", () => this.editSelectedIndex(), query.containerSelector);
        this.createKeyboardShortcut("alt+c", () => this.focusOnQuery(), query.containerSelector);
        this.createKeyboardShortcut("alt+r", () => this.runQuery(), query.containerSelector); // Using keyboard shortcut here, rather than HTML's accesskey, so that we don't steal focus from the editor.
        */

        popoverUtils.longWithHover($(".query-title small"),
            {
                content: '<p>Queries use Lucene syntax. Examples:</p><pre><span class="token keyword">Name</span>: Hi?berna*<br/><span class="token keyword">Count</span>: [0 TO 10]<br/><span class="token keyword">Title</span>: "RavenDb Queries 1010" <span class="token keyword">AND Price</span>: [10.99 TO *]</pre>'
            });
       
        this.registerDisposableHandler($(window), "storage", () => this.loadRecentQueries());
    }

    compositionComplete() {
        super.compositionComplete();

        this.setupDisableReasons();

        const grid = this.gridController();
        grid.withEvaluationContext(this.customFunctionsContext);

        const documentsProvider = new documentBasedColumnsProvider(this.activeDatabase(), grid, this.collections().map(x => x.name), {
            enableInlinePreview: true
        });

        if (!this.fetcher())
            this.fetcher(() => $.when({
                items: [] as document[],
                totalResultCount: 0
            }));

        this.columnsSelector.init(grid,
            (s, t, c) => this.fetcher()(s, t),
            (w, r) => documentsProvider.findColumns(w, r), (results: pagedResult<document>) => documentBasedColumnsProvider.extractUniquePropertyNames(results));

        grid.headerVisible(true);

        grid.dirtyResults.subscribe(dirty => this.dirtyResult(dirty));

        this.fetcher.subscribe(() => grid.reset());

        this.columnPreview.install("virtual-grid", ".tooltip", (doc: document, column: virtualColumn, e: JQueryEventObject, onValue: (context: any) => void) => {
            if (column instanceof textColumn) {
                const value = column.getCellValue(doc);
                if (!_.isUndefined(value)) {
                    const json = JSON.stringify(value, null, 4);
                    const html = Prism.highlight(json, (Prism.languages as any).javascript);
                    onValue(html);
                }
            }
        });
    }

    private loadRecentQueries() {
        recentQueriesStorage.getRecentQueriesWithIndexNameCheck(this.activeDatabase())
            .done(queries => this.recentQueries(queries));
    }

    private fetchAllTransformers(db: database): JQueryPromise<Array<Raven.Client.Documents.Transformers.TransformerDefinition>> {
        return new getTransformersCommand(db)
            .execute()
            .done(transformers => this.allTransformers(transformers));
    }

    private fetchAllCollections(db: database): JQueryPromise<collectionsStats> {
        return new getCollectionsStatsCommand(db)
            .execute()
            .done((results: collectionsStats) => {
                this.collections(results.collections);
            });
    }

    private fetchCustomFunctions(db: database): JQueryPromise<customFunctions> {
        return new getCustomFunctionsCommand(db)
            .execute()
            .done(functions => {
                this.customFunctionsContext = evaluationContextHelper.createContext(functions.functions);
            });
    }

    private fetchAllIndexes(db: database): JQueryPromise<any> {
        return new getDatabaseStatsCommand(db)
            .execute()
            .done((results: Raven.Client.Documents.Operations.DatabaseStatistics) => {
                this.indexes(results.Indexes.map(indexDto => {
                    return {
                        name: indexDto.Name,
                        isMapReduce: indexDto.Type === "MapReduce" //TODO: support for autoindexes?
                    } as indexItem;
                }));
            });
    }

    selectInitialQuery(indexNameOrRecentQueryHash: string) {
        if (!indexNameOrRecentQueryHash) {
            // if no index exists ==> use the default All Documents
            this.setSelectedIndex(queryUtil.AllDocs);
        } else if (this.indexes().find(i => i.name === indexNameOrRecentQueryHash) ||
            indexNameOrRecentQueryHash.startsWith(queryUtil.DynamicPrefix)) {
            this.setSelectedIndex(indexNameOrRecentQueryHash);
        } else if (indexNameOrRecentQueryHash.indexOf("recentquery-") === 0) {
            const hash = parseInt(indexNameOrRecentQueryHash.substr("recentquery-".length), 10);
            const matchingQuery = this.recentQueries().find(q => q.hash === hash);
            if (matchingQuery) {
                this.runRecentQuery(matchingQuery);
            } else {
                this.navigate(appUrl.forQuery(this.activeDatabase()));
            }
        } else if (indexNameOrRecentQueryHash) {
            messagePublisher.reportError(`Could not find index or recent query: ${indexNameOrRecentQueryHash}`);
            // fallback to All Documents, but show error
            this.setSelectedIndex(queryUtil.AllDocs);
        }
    }

    setSelectedIndex(indexName: string) {
        this.initialSelectedIndex = indexName;
        this.criteria().setSelectedIndex(indexName);
        this.uiTransformer(null);
        this.uiTransformerParameters([]);

        this.columnsSelector.reset();

        if (this.isDynamicIndex() && this.criteria().indexEntries()) {
            this.criteria().indexEntries(false);
            this.indexEntrieStateWasTrue = true; // save the state..
        }

        if ((!this.isDynamicIndex() && this.indexEntrieStateWasTrue)) {
            this.criteria().indexEntries(true);
            this.indexEntrieStateWasTrue = false;
        }

        this.runQuery();

        const indexQuery = indexName;
        const url = appUrl.forQuery(this.activeDatabase(), indexQuery);
        this.updateUrl(url);
    }

    private generateQuerySummary() {
        const criteria = this.criteria();
        const transformer = criteria.transformer();
        const transformerPart = transformer ? "transformed by " + transformer : "";
        return transformerPart;
    }

    runQuery() {
        eventsCollector.default.reportEvent("query", "run");
        this.querySummary(this.generateQuerySummary());
        const criteria = this.criteria();

        if (this.criteria().queryText()) {

            //TODO: this.isWarning(false);
            this.isLoading(true);

            const database = this.activeDatabase();

            //TODO: this.currentColumnsParams().enabled(this.showFields() === false && this.indexEntries() === false);

            const queryCmd = new queryCommand(database, 0, 25, this.criteria(), !this.cacheEnabled());

            this.rawJsonUrl(appUrl.forDatabaseQuery(database) + queryCmd.getUrl());
            this.csvUrl(queryCmd.getCsvUrl());

            const resultsFetcher = (skip: number, take: number) => {
                const command = new queryCommand(database, skip, take, this.criteria(), !this.cacheEnabled());
                return command.execute()
                    .always(() => {
                        this.isLoading(false);
                    })
                    .done((queryResults: pagedResult<any>) => {
                        this.queryStats(queryResults.additionalResultInfo);
                        queryUtil.fetchIndexFields(this.activeDatabase(), this.selectedIndex(), this.indexFields);

                        //TODO: this.indexSuggestions([]);
                        /* TODO
                        if (queryResults.totalResultCount == 0) {
                            var queryFields = this.extractQueryFields();
                            var alreadyLookedForNull = false;
                            if (this.selectedIndex().indexOf(this.dynamicPrefix) !== 0) {
                                alreadyLookedForNull = true;
                                for (var i = 0; i < queryFields.length; i++) {
                                    this.getIndexSuggestions(selectedIndex, queryFields[i]);
                                    if (queryFields[i].FieldValue == 'null') {
                                        this.isWarning(true);
                                        this.warningText(<any>("The Query contains '" + queryFields[i].FieldName + ": null', this will check if the field contains the string 'null', is this what you meant?"));
                                    }
                                }
                            }
                            if (!alreadyLookedForNull) {
                                for (var i = 0; i < queryFields.length; i++) {
                                    ;
                                    if (queryFields[i].FieldValue == 'null') {
                                        this.isWarning(true);
                                        this.warningText(<any>("The Query contains '" + queryFields[i].FieldName + ": null', this will check if the field contains the string 'null', is this what you meant?"));
                                    }
                                }
                            }
                        }*/
                    })
                    .fail((request: JQueryXHR) => {
                        const queryText = this.criteria().queryText();
                        recentQueriesStorage.removeRecentQueryByQueryText(database, queryText);
                        this.recentQueries.shift();
                    });
            };

            this.fetcher(resultsFetcher);
            this.recordQueryRun(this.criteria());
        }
    }

    refresh() {
        this.gridController().reset(true);
    }
    
    openQueryStats() {
        //TODO: work on explain in dialog
        eventsCollector.default.reportEvent("query", "show-stats");
        const viewModel = new queryStatsDialog(this.queryStats(), this.activeDatabase());
        app.showBootstrapDialog(viewModel);
    }

    private recordQueryRun(criteria: queryCriteria) {
        const newQuery: storedQueryDto = criteria.toStorageDto();

        const queryUrl = appUrl.forQuery(this.activeDatabase(), newQuery.hash);
        this.updateUrl(queryUrl);

        recentQueriesStorage.appendQuery(newQuery, this.recentQueries);
        recentQueriesStorage.saveRecentQueries(this.activeDatabase(), this.recentQueries());
    }

    runRecentQuery(storedQuery: storedQueryDto) {
        eventsCollector.default.reportEvent("query", "run-recent");

        const criteria = this.criteria();

        criteria.updateUsing(storedQuery);

        const matchedTransformer = this.allTransformers().find(t => t.Name === criteria.transformer());

        if (matchedTransformer) {
            this.selectTransformer(matchedTransformer);
            this.fillTransformerParameters(criteria.transformerParameters());
        } else {
            this.selectTransformer(null);
        }

        this.runQuery();
    }

    private fillTransformerParameters(transformerParameters: Array<transformerParamDto>) {
        transformerParameters.forEach(param => {
            const matchingField = this.uiTransformerParameters().find(x => x.name === param.name);
            if (matchingField) {
                matchingField.value(param.value);
            }
        });
    }

    selectTransformer(transformer: Raven.Client.Documents.Transformers.TransformerDefinition) {
        if (transformer) {
            this.uiTransformer(transformer.Name);
            const inputs = transformerType.extractInputs(transformer.TransformResults);
            this.uiTransformerParameters(inputs.map(input => new queryTransformerParameter(input)));
        } else {
            this.uiTransformer(null);
            this.uiTransformerParameters([]);
        }
    }

    private validateTransformer(): boolean {
        if (!this.uiTransformer() || this.uiTransformerParameters().length === 0) {
            return true;
        }

        let valid = true;

        this.uiTransformerParameters().forEach(param => {
            if (!this.isValid(param.validationGroup)) {
                valid = false;
            }
        });

        return valid;
    }

    applyTransformer() {
        if (this.validateTransformer()) {

            $("transform-results-btn").dropdown("toggle");

            const criteria = this.criteria();
            const transformerToApply = this.uiTransformer();
            if (transformerToApply) {
                criteria.transformer(transformerToApply);
                criteria.transformerParameters(this.uiTransformerParameters().map(p => p.toDto()));
                this.runQuery();
            } else {
                criteria.transformer(null);
                criteria.transformerParameters([]);
                this.runQuery();
            }
        }
    }

    getStoredQueryTransformerParameters(queryParams: Array<transformerParamDto>): string {
        if (queryParams.length > 0) {
            return "(" +
                queryParams
                    .map((param: transformerParamDto) => param.name + "=" + param.value)
                    .join(", ") + ")";
        }

        return "";
    }

    getRecentQuerySortText(sorts: string[]) {
        if (sorts.length > 0) {
            return sorts
                .map(s => querySort.fromQuerySortString(s).toHumanizedString())
                .join(", ");
        }

        return "";
    }

    queryCompleter(editor: any, session: any, pos: AceAjax.Position, prefix: string, callback: (errors: any[], worldlist: { name: string; value: string; score: number; meta: string }[]) => void) {
        queryUtil.queryCompleter(this.indexFields, this.selectedIndex, this.activeDatabase, editor, session, pos, prefix, callback);
        callback([], []);
    }

    deleteDocsMatchingQuery() {
        eventsCollector.default.reportEvent("query", "delete-documents");
        // Run the query so that we have an idea of what we'll be deleting.
        this.runQuery();
        this.fetcher()(0, 1)
            .done((results) => {
                if (results.totalResultCount === 0) {
                    app.showBootstrapMessage("There are no documents matching your query.", "Nothing to do");
                } else {
                    this.promptDeleteDocsMatchingQuery(results.totalResultCount);
                }
            });
    }

    private promptDeleteDocsMatchingQuery(resultCount: number) {
        const criteria = this.criteria();
        const db = this.activeDatabase();
        const viewModel = new deleteDocumentsMatchingQueryConfirm(this.selectedIndex(), criteria.queryText(), resultCount, db);
        app.showBootstrapDialog(viewModel)
           .done((result) => {
                if (result) {
                    new deleteDocsMatchingQueryCommand(criteria.queryText(), this.activeDatabase())
                        .execute()
                        .done((operationId: operationIdDto) => {
                            this.monitorDeleteOperation(db, operationId.OperationId);
                        });
                }
           });
    }

    private monitorDeleteOperation(db: database, operationId: number) {
        notificationCenter.instance.openDetailsForOperationById(db, operationId);

        notificationCenter.instance.monitorOperation(db, operationId)
            .done(() => {
                messagePublisher.reportSuccess("Successfully deleted documents");
                this.refresh();
            })
            .fail((exception: Raven.Client.Documents.Operations.OperationExceptionResult) => {
                messagePublisher.reportError("Could not delete documents: " + exception.Message, exception.Error, null, false);
            });
    }

    /* TODO
    extractQueryFields(): Array<queryFieldInfo> {
        var query = this.queryText();
        var luceneSimpleFieldRegex = /(\w+):\s*("((?:[^"\\]|\\.)*)"|'((?:[^'\\]|\\.)*)'|(\w+))/g;

        var queryFields: Array<queryFieldInfo> = [];
        var match: RegExpExecArray = null;
        while ((match = luceneSimpleFieldRegex.exec(query))) {
            var value = match[3] || match[4] || match[5];
            queryFields.push({
                FieldName: match[1],
                FieldValue: value,
                Index: match.index
            });
        }
        return queryFields;
    }
   
*/

    /* TODO future:

     getIndexSuggestions(indexName: string, info: queryFieldInfo) {
        if (_.includes(this.indexFields(), info.FieldName)) {
            var task = new getIndexSuggestionsCommand(this.activeDatabase(), indexName, info.FieldName, info.FieldValue).execute();
            task.done((result: suggestionsDto) => {
                for (var index = 0; index < result.Suggestions.length; index++) {
                    this.indexSuggestions.push({
                        Index: info.Index,
                        FieldName: info.FieldName,
                        FieldValue: info.FieldValue,
                        Suggestion: result.Suggestions[index]
                    });
                }
            });
        }
    }

    applySuggestion(suggestion: indexSuggestion) {
        eventsCollector.default.reportEvent("query", "apply-suggestion");
        var value = this.queryText();
        var startIndex = value.indexOf(suggestion.FieldValue, suggestion.Index);
        this.queryText(value.substring(0, startIndex) + suggestion.Suggestion + value.substring(startIndex + suggestion.FieldValue.length));
        this.indexSuggestions([]);
        this.runQuery();
    }

      exportCsv() {
        eventsCollector.default.reportEvent("query", "export-csv");

        var db = this.activeDatabase();
        var url = appUrl.forDatabaseQuery(db) + this.csvUrl();
        this.downloader.download(db, url);
    }

     onIndexChanged(newIndexName: string) {
        var command = getCustomColumnsCommand.forIndex(newIndexName, this.activeDatabase());
        this.contextName(command.docName);

        command.execute().done((dto: customColumnsDto) => {
            if (dto) {
                this.currentColumnsParams().columns($.map(dto.Columns, c => new customColumnParams(c)));
                this.currentColumnsParams().customMode(true);
            } else {
                // use default values!
                this.currentColumnsParams().columns.removeAll();
                this.currentColumnsParams().customMode(false);
            }

        });
    }
    */

}
export = query;