import appUrl from "common/appUrl";
import viewModelBase from "viewmodels/viewModelBase";
import router from "plugins/router";
import fileImporter from "common/fileImporter";
import aceEditorBindingHandler from "common/bindingHelpers/aceEditorBindingHandler";
import customSorter from "models/database/settings/customSorter";
import messagePublisher from "common/messagePublisher";
import getServerWideCustomSortersCommand from "commands/serverWide/sorters/getServerWideCustomSortersCommand";
import saveServerWideCustomSorterCommand from "commands/serverWide/sorters/saveServerWideCustomSorterCommand";

class editServerWideCustomSorter extends viewModelBase {

    view = require("views/manage/editServerWideCustomSorter.html");

    editedServerWideSorter = ko.observable<customSorter>();

    usedSorterNames = ko.observableArray<string>([]);

    isAddingNewSorter = ko.observable<boolean>();

    spinners = {
        save: ko.observable<boolean>(false)
    };

    constructor() {
        super();
        aceEditorBindingHandler.install();
    }

    activate(args: any) {
        super.activate(args);

        return new getServerWideCustomSortersCommand()
            .execute()
            .then(sorters => {
                this.isAddingNewSorter(!args);

                if (args && args.sorterName) {
                    const matchedSorter = sorters.find(x => x.Name === args.sorterName);
                    if (matchedSorter) {
                        this.editedServerWideSorter(new customSorter(matchedSorter));
                        this.dirtyFlag = this.editedServerWideSorter().dirtyFlag;
                    } else {
                        messagePublisher.reportWarning("Unable to find server-wide custom sorter named: " + args.sorterName);
                        router.navigate(appUrl.forServerWideCustomSorters());

                        return false;
                    }
                } else {
                    this.usedSorterNames(sorters.map(x => x.Name));
                    this.editedServerWideSorter(customSorter.empty());
                    this.dirtyFlag = this.editedServerWideSorter().dirtyFlag;
                }
            });
    }

    cancelOperation() {
        this.goToServerWideCustomSortersView();
    }

    private goToServerWideCustomSortersView() {
        router.navigate(appUrl.forServerWideCustomSorters());
    }

    fileSelected(fileInput: HTMLInputElement) {
        fileImporter.readAsText(fileInput, (data, fileName) => {
            this.editedServerWideSorter().code(data);

            if (fileName.endsWith(".cs")) {
                fileName = fileName.substr(0, fileName.length - 3);
            }

            if (this.isAddingNewSorter()) {
                this.editedServerWideSorter().name(fileName);
            }
        });
    }

    save() {
        if (this.isValid(this.editedServerWideSorter().validationGroup)) {
            this.spinners.save(true);

            new saveServerWideCustomSorterCommand(this.editedServerWideSorter().toDto())
                .execute()
                .done(() => {
                    this.dirtyFlag().reset();
                    this.goToServerWideCustomSortersView();
                })
                .always(() => this.spinners.save(false));
        }
    }
}

export = editServerWideCustomSorter;
