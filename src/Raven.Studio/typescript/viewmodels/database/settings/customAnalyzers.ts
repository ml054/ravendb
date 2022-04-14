import viewModelBase from "viewmodels/viewModelBase";
import appUrl from "common/appUrl";
import getCustomAnalyzersCommand from "commands/database/settings/getCustomAnalyzersCommand";
import deleteCustomAnalyzerCommand from "commands/database/settings/deleteCustomAnalyzerCommand";
import getServerWideCustomAnalyzersCommand from "commands/serverWide/analyzers/getServerWideCustomAnalyzersCommand";
import database from "models/resources/database";
import router from "plugins/router";
import aceEditorBindingHandler from "common/bindingHelpers/aceEditorBindingHandler";
import generalUtils from "common/generalUtils";
import analyzerListItemModel from "models/database/settings/analyzerListItemModel";
import accessManager from "common/shell/accessManager";
import shardViewModelBase from "viewmodels/shardViewModelBase";

class customAnalyzers extends shardViewModelBase {

    view = require("views/database/settings/customAnalyzers.html");
    
    analyzers = ko.observableArray<analyzerListItemModel>([]);
    serverWideAnalyzers = ko.observableArray<analyzerListItemModel>([]);
    
    addUrl = ko.pureComputed(() => appUrl.forEditCustomAnalyzer(this.db));
    
    serverWideCustomAnalyzersUrl = appUrl.forServerWideCustomAnalyzers();
    canNavigateToServerWideCustomAnalyzers: KnockoutComputed<boolean>;
    
    clientVersion = viewModelBase.clientVersion;
    
    constructor(db: database) {
        super(db);
        
        aceEditorBindingHandler.install();
        this.bindToCurrentInstance("confirmRemoveAnalyzer", "editAnalyzer");

        this.canNavigateToServerWideCustomAnalyzers = accessManager.default.isClusterAdminOrClusterNode;
    }
    
    activate(args: any) {
        super.activate(args);
        
        return $.when<any>(this.loadAnalyzers(), this.loadServerWideAnalyzers())
            .done(() => {
                const serverWideAnalyzerNames = this.serverWideAnalyzers().map(x => x.name);
                
                this.analyzers().forEach(analyzer => {
                    if (_.includes(serverWideAnalyzerNames, analyzer.name)) {
                        analyzer.overrideServerWide(true);
                    }
                })
            })
    }
    
    private loadAnalyzers() {
        return new getCustomAnalyzersCommand(this.db)
            .execute()
            .done(analyzers => this.analyzers(analyzers.map(x => new analyzerListItemModel(x))));
    }

    private loadServerWideAnalyzers() {
        return new getServerWideCustomAnalyzersCommand()
            .execute()
            .done(analyzers => this.serverWideAnalyzers(analyzers.map(x => new analyzerListItemModel(x))));
    }

    compositionComplete() {
        super.compositionComplete();

        $('.custom-analyzers [data-toggle="tooltip"]').tooltip();
    }
    
    editAnalyzer(analyzer: analyzerListItemModel) {
        const url = appUrl.forEditCustomAnalyzer(this.db, analyzer.name);
        router.navigate(url);
    }
    
    confirmRemoveAnalyzer(analyzer: analyzerListItemModel) {
        this.confirmationMessage("Delete Custom Analyzer",
            `You're deleting custom analyzer: <br><ul><li><strong>${generalUtils.escapeHtml(analyzer.name)}</strong></li></ul>`, {
            buttons: ["Cancel", "Delete"],
            html: true
        })
            .done(result => {
                if (result.can) {
                    this.analyzers.remove(analyzer);
                    this.deleteAnalyzer(this.db, analyzer.name);
                }
            })
    }
    
    private deleteAnalyzer(db: database, name: string) {
        return new deleteCustomAnalyzerCommand(db, name)
            .execute()
            .always(() => {
                this.loadAnalyzers();
            })
    }
}

export = customAnalyzers;
