import appUrl from "common/appUrl";
import viewModelBase from "viewmodels/viewModelBase";
import sorterListItemModel from "models/database/settings/sorterListItemModel";
import aceEditorBindingHandler from "common/bindingHelpers/aceEditorBindingHandler";
import generalUtils from "common/generalUtils";
import router from "plugins/router";
import getServerWideCustomSortersCommand from "commands/serverWide/sorters/getServerWideCustomSortersCommand";
import deleteServerWideCustomSorterCommand from "commands/serverWide/sorters/deleteServerWideCustomSorterCommand";

class serverWideCustomSorters extends viewModelBase {

    view = require("views/manage/serverWideCustomSorters.html");
    
    serverWideSorters = ko.observableArray<sorterListItemModel>([]);

    addUrl = ko.pureComputed(() => appUrl.forEditServerWideCustomSorter());
    
    clientVersion = viewModelBase.clientVersion;

    constructor() {
        super();

        aceEditorBindingHandler.install();
        this.bindToCurrentInstance("confirmRemoveServerWideSorter", "editServerWideSorter");
    }

    activate(args: any) {
        super.activate(args);
        
        return this.loadServerWideSorters();
    }

    private loadServerWideSorters() {
        return new getServerWideCustomSortersCommand()
            .execute()
            .done(sorters => {
                this.serverWideSorters(sorters.map(x => new sorterListItemModel(x)));
            });
    }

    editServerWideSorter(sorter: sorterListItemModel) {
        const url = appUrl.forEditServerWideCustomSorter(sorter.name);
        router.navigate(url);
    }

    confirmRemoveServerWideSorter(sorter: sorterListItemModel) {
        this.confirmationMessage("Delete Server-Wide Custom Sorter",
            `You're deleting server-wide custom sorter: <br><ul><li><strong>${generalUtils.escapeHtml(sorter.name)}</strong></li></ul>`, {
                buttons: ["Cancel", "Delete"],
                html: true
            })
            .done(result => {
                if (result.can) {
                    this.serverWideSorters.remove(sorter);
                    this.deleteServerWideSorter(sorter.name);
                }
            })
    }   

    private deleteServerWideSorter(name: string) {
        return new deleteServerWideCustomSorterCommand(name)
            .execute()
            .always(() => {
                this.loadServerWideSorters();
            })
    }
}

export = serverWideCustomSorters;
