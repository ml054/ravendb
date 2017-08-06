import viewModelBase = require("viewmodels/viewModelBase");
import appUrl = require("common/appUrl");
import getDatabaseStatsCommand = require("commands/resources/getDatabaseStatsCommand");
import getIndexDefinitionCommand = require("commands/database/index/getIndexDefinitionCommand");
import facet = require("models/database/query/facet");
import queryFacetsCommand = require("commands/database/query/queryFacetsCommand");
import aceEditorBindingHandler = require("common/bindingHelpers/aceEditorBindingHandler");
import eventsCollector = require("common/eventsCollector");
import popoverUtils = require("common/popoverUtils");

class reporting extends viewModelBase {
    selectedIndexName = ko.observable<string>();
    selectedIndexLabel = ko.computed(() => this.selectedIndexName() ? this.selectedIndexName() : "[Select an index]");
    indexNames = ko.observableArray<string>();
    hasSelectedIndex = ko.computed(() => this.selectedIndexName() && this.selectedIndexName().length > 0);
    editSelectedIndexUrl = ko.computed(() => this.hasSelectedIndex() ? appUrl.forEditIndex(this.selectedIndexName(), this.activeDatabase()) : null);
    availableFields = ko.observableArray<string>();
    sortOptions = ko.observableArray<any>();
    selectedField = ko.observable<string>();
    selectedFieldLabel = ko.computed(() => this.selectedField() ? this.selectedField() : "Select a field");
    addedValues = ko.observableArray<facet>();
    filter = ko.observable<string>();
    hasFilter = ko.observable(false);
    reportResults = ko.observable<any>(); //TODO: use type
    totalQueryResults = ko.computed(() => this.reportResults() ? this.reportResults().totalResultCount() : null);
    queryDuration = ko.observable<string>();
    appUrls: computedAppUrls;
    isCacheDisable = ko.observable<boolean>(false);
    isExportEnabled = ko.computed(() => this.reportResults() ? this.reportResults().totalResultCount() > 0 : false);

    exportCsv() {
        eventsCollector.default.reportEvent("reporting", "export-csv");

        if (this.isExportEnabled() === false)
            return false;

        var objArray = JSON.stringify(this.reportResults().getAllCachedItems());
        var array = typeof objArray != 'object' ? JSON.parse(objArray) : objArray;

        if (array[0] === undefined)
            return false;

        var str = '';

        var line = '';
        for (var header in array[0]) {
            if (header === "__metadata")
                continue;
            if (line) line += ',';

            line += header;
        }

        str += line + '\r\n';

        for (var i = 0; i < array.length; i++) {
            line = '';
            for (var index in array[i]) {
                if (index === "__metadata")
                    continue;
                if (line) line += ',';

                line += array[i][index];
            }

            str += line + '\r\n';
        }

        var uriContent = encodeURIComponent(str);
        var link = document.createElement('a');
        (<any>link)["download"] = this.selectedIndexName() ? "Reporting_" + this.selectedIndexName() + ".csv" : "reporting.csv";
        link.href = 'data:,' + uriContent;
        link.click();
        return true;
    }

    attached() {
        super.attached();
        
        popoverUtils.longWithHover($("#filterQueryLabel"), {
            content: '<p>Queries use Lucene syntax. Examples:</p><pre><span class="token keyword">Name</span>: Hi?berna*<br/><span class="token keyword">Count</span>: [0 TO 10]<br/><span class="token keyword">Title</span>: "RavenDb Queries 1010" <span class="token keyword">AND Price</span>: [10.99 TO *]</pre>'
        });
    }

    activate(indexToActivateOrNull: string) {
        super.activate(indexToActivateOrNull);
        this.updateHelpLink('O3EA1R');

        this.fetchIndexes().done(() => this.selectInitialIndex(indexToActivateOrNull));
        this.selectedIndexName.subscribe(() => this.resetSelections());

        aceEditorBindingHandler.install();
    }

    fetchIndexes(): JQueryPromise<any> {
        return new getDatabaseStatsCommand(this.activeDatabase())
            .execute()
            .done((results) => this.indexNames(results.Indexes.map(i => i.Name)));
    }

    fetchIndexDefinition(indexName: string) {
        new getIndexDefinitionCommand(indexName, this.activeDatabase())
            .execute()
            .done((dto) => {
                /* TODO this.sortOptions(dto.Index.SortOptions);
                this.availableFields(dto.Index.Fields);*/
            });
    }

    selectInitialIndex(indexToActivateOrNull: string) {
        if (indexToActivateOrNull && _.includes(this.indexNames(), indexToActivateOrNull)) {
            this.setSelectedIndex(indexToActivateOrNull);
        } else if (this.indexNames().length > 0) {
            this.setSelectedIndex(_.first(this.indexNames()));
        }
    }

    setSelectedIndex(indexName: string) {
        this.selectedIndexName(indexName);
        this.updateUrl(appUrl.forReporting(this.activeDatabase(), indexName));

        this.fetchIndexDefinition(indexName);
    }

    setSelectedField(fieldName: string) {
        this.selectedField(fieldName);

        // Update all facets to use that too.
        this.addedValues().forEach(v => v.name = fieldName);
    }

    resetSelections() {
        this.selectedField(null);
        this.addedValues([]);
        this.availableFields([]);
        if (!!this.reportResults()) {
            this.reportResults(null);
        }
    }

    mapSortToType(sort: string) {
        switch (sort) {
            case 'Int':
                return "System.Int32";
            case 'Float':
                return "System.Single";
            case 'Long':
                return 'System.Int64';
            case 'Double':
                return "System.Double";
            case 'Short':
                return 'System.Int16';
            case 'Byte':
                return 'System.Byte';
            default: 
                return 'System.String';
        }
    }

    addValue(fieldName: string) {
        eventsCollector.default.reportEvent("reporting", "add-value");

        var sortOps = this.sortOptions();
        var sortOption = (fieldName in sortOps) ? (<any>sortOps)[fieldName] : "String";
        var val = facet.fromNameAndAggregation(this.selectedField(), fieldName, this.mapSortToType(sortOption));
        this.addedValues.push(val);
    }

    removeValue(val: facet) {
        eventsCollector.default.reportEvent("reporting", "remove-value");

        this.addedValues.remove(val);
    }

    runReport() {
        eventsCollector.default.reportEvent("reporting", "run");

        var selectedIndex = this.selectedIndexName();
        var filterQuery = this.hasFilter() ? this.filter() : null;
        var facets = this.addedValues().map(v => v.toDto());
        var groupedFacets: facetDto[] = [];
        facets.forEach((curFacet) => {
            var foundFacet = groupedFacets.find(x => x.AggregationField == curFacet.AggregationField);

            if (foundFacet) {
                foundFacet.Aggregation += curFacet.Aggregation;
            } else {
                groupedFacets.push(curFacet);
            }
        });
        var db = this.activeDatabase();
        var resultsFetcher = (skip: number, take: number) => {
            var command = new queryFacetsCommand(selectedIndex, filterQuery, skip, take, groupedFacets, db, this.isCacheDisable());
            return command.execute()
                .done((resultSet: pagedResult<any>) => this.queryDuration(resultSet.additionalResultInfo));
        };
        //TODO: this.reportResults(new pagedList(resultsFetcher));
    }

    toggleCacheEnable() {
        eventsCollector.default.reportEvent("reporting", "toggle-cache");

        this.isCacheDisable(!this.isCacheDisable());
    }

}

export = reporting;
