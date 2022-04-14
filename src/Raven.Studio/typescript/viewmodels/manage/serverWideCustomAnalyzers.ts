import appUrl from "common/appUrl";
import viewModelBase from "viewmodels/viewModelBase";
import analyzerListItemModel from "models/database/settings/analyzerListItemModel";
import aceEditorBindingHandler from "common/bindingHelpers/aceEditorBindingHandler";
import getServerWideCustomAnalyzersCommand from "commands/serverWide/analyzers/getServerWideCustomAnalyzersCommand";
import router from "plugins/router";
import generalUtils from "common/generalUtils";
import deleteServerWideCustomAnalyzerCommand from "commands/serverWide/analyzers/deleteServerWideCustomAnalyzerCommand";

class serverWideCustomAnalyzers extends viewModelBase {

    view = require("views/manage/serverWideCustomAnalyzers.html");
    
    serverWideAnalyzers = ko.observableArray<analyzerListItemModel>([]);

    addUrl = ko.pureComputed(() => appUrl.forEditServerWideCustomAnalyzer());

    clientVersion = viewModelBase.clientVersion;

    constructor() {
        super();

        aceEditorBindingHandler.install();
        this.bindToCurrentInstance("confirmRemoveServerWideAnalyzer", "editServerWideAnalyzer");
    }

    activate(args: any) {
        super.activate(args);

        return this.loadServerWideAnalyzers();
    }

    private loadServerWideAnalyzers() {
        return new getServerWideCustomAnalyzersCommand()
            .execute()
            .done(analyzers => {
                this.serverWideAnalyzers(analyzers.map(x => new analyzerListItemModel(x)));
            });
    }

    editServerWideAnalyzer(analyzer: analyzerListItemModel) {
        const url = appUrl.forEditServerWideCustomAnalyzer(analyzer.name);
        router.navigate(url);
    }

    confirmRemoveServerWideAnalyzer(analyzer: analyzerListItemModel) {
        this.confirmationMessage("Delete Server-Wide Custom Analyzer",
            `You're deleting server-wide custom analyzer: <br><ul><li><strong>${generalUtils.escapeHtml(analyzer.name)}</strong></li></ul>`, {
                buttons: ["Cancel", "Delete"],
                html: true
            })
            .done(result => {
                if (result.can) {
                    this.serverWideAnalyzers.remove(analyzer);
                    this.deleteServerWideAnalyzer(analyzer.name);
                }
            })
    }

    private deleteServerWideAnalyzer(name: string) {
        return new deleteServerWideCustomAnalyzerCommand(name)
            .execute()
            .always(() => {
                this.loadServerWideAnalyzers();
            })
    }
}

export = serverWideCustomAnalyzers;
