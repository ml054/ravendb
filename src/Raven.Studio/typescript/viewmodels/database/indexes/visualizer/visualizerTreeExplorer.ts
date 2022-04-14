import generalUtils from "common/generalUtils";
import virtualGridController from "widgets/virtualGrid/virtualGridController";
import textColumn from "widgets/virtualGrid/columns/textColumn";
import columnPreviewPlugin from "widgets/virtualGrid/columnPreviewPlugin";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import aceEditorBindingHandler from "common/bindingHelpers/aceEditorBindingHandler";
import { highlight, languages } from "prismjs";

class visualizerTreeExplorer extends dialogViewModelBase {

    view = require("views/database/indexes/visualizer/visualizerTreeExplorer.html");

    private tableItems: Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf[] = [];
    private gridController = ko.observable<virtualGridController<Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf>>();
    private columnPreview = new columnPreviewPlugin<Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf>();

    private dto: Raven.Server.Documents.Indexes.Debugging.ReduceTreePage;
    private aggregationResult = ko.observable<string>();
    private hasEntries: boolean;

    constructor(dto: Raven.Server.Documents.Indexes.Debugging.ReduceTreePage) {
        super();
        this.tableItems = dto.Entries;
        this.aggregationResult(JSON.stringify(dto.AggregationResult, null, 4));
        this.hasEntries = !!dto.Entries;

        aceEditorBindingHandler.install();
    }

    compositionComplete() {
        super.compositionComplete();

        if (this.hasEntries) {
            const grid = this.gridController();
            grid.headerVisible(true);

            grid.init((s, t) => this.fetcher(s, t), () => this.findColumns());

            this.columnPreview.install(".visualiserTreeExplorer", ".js-visualizer-tree-tooltip",
                (details: Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf, column: textColumn<Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf>,
                 e: JQueryEventObject, onValue: (context: any, valueToCopy: string) => void) => {
                    const value = column.getCellValue(details);
                    if (!_.isUndefined(value)) {
                        const json = JSON.stringify(value, null, 4);
                        const html = highlight(json, languages.javascript, "js");
                        onValue(html, json);
                    }
                });
        }
    }

    private findColumns() {
        const keys = Object.keys(this.tableItems[0].Data);
        
        const columns = keys.map(key => {
            return new textColumn<Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf>(this.gridController(), x => x.Data[key], generalUtils.escapeHtml(key), (80 / keys.length) + "%");
        });

        columns.push(new textColumn<Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf>(this.gridController(), x => x.Source || '-', "Source Document", "20%"));
        return columns;
    }

    private fetcher(skip: number, take: number): JQueryPromise<pagedResult<Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf>> {
        return $.Deferred<pagedResult<Raven.Server.Documents.Indexes.Debugging.MapResultInLeaf>>()
            .resolve({
                items: this.tableItems,
                totalResultCount: this.tableItems.length
            });
    }

}

export = visualizerTreeExplorer;
