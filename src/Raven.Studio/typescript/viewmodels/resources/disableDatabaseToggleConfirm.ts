import confirmViewModelBase = require("viewmodels/confirmViewModelBase");
import databases = require("components/models/databases");

class disableDatabaseToggleConfirm extends confirmViewModelBase<confirmDialogResult> {

    view = require("views/resources/disableDatabaseToggleConfirm.html");

    desiredAction = ko.observable<string>();
    deletionText: string;
    confirmDeletionText: string;

    private readonly databases: databases.DatabaseSharedInfo[];

    private readonly disable: boolean;

    constructor(databases: databases.DatabaseSharedInfo[], disable: boolean) {
        super(null);
        this.disable = disable;
        this.databases = databases;

        this.deletionText = disable ? "You're disabling" : "You're enabling";
        this.confirmDeletionText = disable ? "Disable" : "Enable";
    }
}

export = disableDatabaseToggleConfirm;
