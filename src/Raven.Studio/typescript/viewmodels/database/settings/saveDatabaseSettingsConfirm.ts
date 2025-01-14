import confirmViewModelBase = require("viewmodels/confirmViewModelBase");
import prismjs = require("prismjs");

class saveDatabaseSettingsConfirm extends confirmViewModelBase<confirmDialogResult> {

    view = require("views/database/settings/saveDatabaseSettingsConfirm.html");

    settingsToSaveText: string;
    
    constructor(private databaseSettingsToSave: object, private howToReloadDatabaseHtml: string) {
        super();

        const settingsJson = JSON.stringify(databaseSettingsToSave, null, 4);
        const settingsHtml = prismjs.highlight(settingsJson, prismjs.languages.javascript, "js")

        this.settingsToSaveText = settingsHtml;
    }

    save() {
        this.confirm();
    }
}

export = saveDatabaseSettingsConfirm;
