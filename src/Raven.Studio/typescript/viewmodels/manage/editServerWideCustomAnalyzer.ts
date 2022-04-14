import appUrl from "common/appUrl";
import viewModelBase from "viewmodels/viewModelBase";
import router from "plugins/router";
import aceEditorBindingHandler from "common/bindingHelpers/aceEditorBindingHandler";
import customAnalyzer from "models/database/settings/customAnalyzer";
import saveServerWideCustomAnalyzerCommand from "commands/serverWide/analyzers/saveServerWideCustomAnalyzerCommand";
import getServerWideCustomAnalyzersCommand from "commands/serverWide/analyzers/getServerWideCustomAnalyzersCommand";
import messagePublisher from "common/messagePublisher";
import fileImporter from "common/fileImporter";
import editCustomAnalyzer from "viewmodels/database/settings/editCustomAnalyzer";

class editServerWideCustomAnalyzer extends viewModelBase {

    view = require("views/manage/editServerWideCustomAnalyzer.html");

    editedServerWideAnalyzer = ko.observable<customAnalyzer>();
    usedAnalyzerNames = ko.observableArray<string>([]);
    isAddingNewAnalyzer = ko.observable<boolean>();

    spinners = {
        save: ko.observable<boolean>(false)
    };

    constructor() {
        super();
        aceEditorBindingHandler.install();
    }

    activate(args: any) {
        super.activate(args);

        return new getServerWideCustomAnalyzersCommand()
            .execute()
            .then((analyzers: Array<Raven.Client.Documents.Indexes.Analysis.AnalyzerDefinition>) => {
                this.isAddingNewAnalyzer(!args);

                if (args && args.analyzerName) {
                    const matchedAnalyzer = analyzers.find(x => x.Name === args.analyzerName);
                    if (matchedAnalyzer) {
                        this.editedServerWideAnalyzer(new customAnalyzer(matchedAnalyzer));
                        this.dirtyFlag = this.editedServerWideAnalyzer().dirtyFlag;
                    } else {
                        messagePublisher.reportWarning("Unable to find server-wide custom analyzer named: " + args.analyzerName);
                        router.navigate(appUrl.forServerWideCustomAnalyzers());

                        return false;
                    }
                } else {
                    this.usedAnalyzerNames(analyzers.map(x => x.Name));
                    this.editedServerWideAnalyzer(customAnalyzer.empty());
                    this.dirtyFlag = this.editedServerWideAnalyzer().dirtyFlag;
                }
            });
    }

    cancelOperation() {
        this.goToServerWideCustomAnalyzersView();
    }

    private goToServerWideCustomAnalyzersView() {
        router.navigate(appUrl.forServerWideCustomAnalyzers());
    }

    fileSelected(fileInput: HTMLInputElement) {
        fileImporter.readAsText(fileInput, (data, fileName) => {
            this.editedServerWideAnalyzer().code(data);

            if (fileName.endsWith(".cs")) {
                fileName = fileName.substr(0, fileName.length - 3);
            }

            if (this.isAddingNewAnalyzer()) {
                this.editedServerWideAnalyzer().name(fileName);
            }
        });
    }

    save() {
        if (this.isValid(this.editedServerWideAnalyzer().validationGroup)) {
            editCustomAnalyzer.maybeShowIndexResetNotice(this.isAddingNewAnalyzer())
                .done(() => {
                    this.spinners.save(true);

                    new saveServerWideCustomAnalyzerCommand(this.editedServerWideAnalyzer().toDto())
                        .execute()
                        .done(() => {
                            this.dirtyFlag().reset();
                            this.goToServerWideCustomAnalyzersView();
                        })
                        .always(() => this.spinners.save(false));
                });
        }
    }
}

export = editServerWideCustomAnalyzer;
