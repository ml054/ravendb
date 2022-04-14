import copyToClipboard from "common/copyToClipboard";
import createSampleDataCommand from "commands/database/studio/createSampleDataCommand";
import createSampleDataClassCommand from "commands/database/studio/createSampleDataClassCommand";
import aceEditorBindingHandler from "common/bindingHelpers/aceEditorBindingHandler";
import eventsCollector from "common/eventsCollector";
import getCollectionsStatsCommand from "commands/database/documents/getCollectionsStatsCommand";
import collectionsStats from "models/database/documents/collectionsStats";
import appUrl from "common/appUrl";
import database from "models/resources/database";
import getDatabaseCommand from "commands/resources/getDatabaseCommand";
import collectionsTracker from "common/helpers/database/collectionsTracker";
import { highlight, languages } from "prismjs";
import shardViewModelBase from "viewmodels/shardViewModelBase";

class createSampleData extends shardViewModelBase {
    
    view = require("views/database/tasks/createSampleData.html");

    classData = ko.observable<string>();
    canCreateSampleData = ko.observable<boolean>(false);
    justCreatedSampleData = ko.observable<boolean>(false);
    classesVisible = ko.observable<boolean>(false);

    classDataFormatted = ko.pureComputed(() => {
        return highlight(this.classData(), languages.javascript, "js");
    });

    constructor(db: database) {
        super(db);
        aceEditorBindingHandler.install();
    }

    generateSampleData() {
        eventsCollector.default.reportEvent("sample-data", "create");
        this.isBusy(true);

        const db = this.db;
        
        new createSampleDataCommand(db)
            .execute()
            .done(() => {
                this.canCreateSampleData(false);
                this.justCreatedSampleData(true);
                this.checkIfRevisionsWasEnabled(db);
            })
            .always(() => this.isBusy(false));
    }
    
    private checkIfRevisionsWasEnabled(db: database) {
        if (!db.hasRevisionsConfiguration()) {
                new getDatabaseCommand(db.name)
                    .execute()
                    .done(dbInfo => {
                        if (dbInfo.HasRevisionsConfiguration) {
                            db.hasRevisionsConfiguration(true);

                            collectionsTracker.default.configureRevisions(db);
                        }
                    })
        }
    }
    

    activate(args: any) {
        super.activate(args);
        this.updateHelpLink('OGRN53');

        return $.when<any>(this.fetchSampleDataClasses(), this.fetchCollectionsStats());
    }

    showCode() {
        this.classesVisible(true);

        const $pageHostRoot = $("#page-host-root");
        const $sampleDataMain = $(".sample-data-main");

        $pageHostRoot.animate({
            scrollTop: $sampleDataMain.height()
        }, 'fast');
    }

    copyClasses() {
        eventsCollector.default.reportEvent("sample-data", "copy-classes");
        copyToClipboard.copy(this.classData(), "Copied C# classes to clipboard.");
    }

    private fetchCollectionsStats() {
        new getCollectionsStatsCommand(this.db)
            .execute()
            .done(stats => this.onCollectionsFetched(stats));
    }

    private onCollectionsFetched(stats: collectionsStats) {
        const nonEmptyNonSystemCollectionsCount = stats
            .collections
            .filter(x => x.documentCount() > 0)
            .length;
        this.canCreateSampleData(nonEmptyNonSystemCollectionsCount === 0);
    }

    private fetchSampleDataClasses(): JQueryPromise<string> {
        return new createSampleDataClassCommand(this.db)
            .execute()
            .done((results: string) => {
                this.classData(results);
            });
    }

    private urlForDatabaseDocuments() {
        return appUrl.forDocuments("", this.db);
    }
}

export = createSampleData; 
