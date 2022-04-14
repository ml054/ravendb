import dialog from "plugins/dialog";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import serverBuildReminder from "common/storage/serverBuildReminder";

class latestBuildReminder extends dialogViewModelBase {

    view = require("views/common/latestBuildReminder.html");

    public dialogTask = $.Deferred();
    mute = ko.observable<boolean>(false);

    constructor(private latestServerBuild: serverBuildVersionDto, elementToFocusOnDismissal?: string) {
        super({ elementToFocusOnDismissal: elementToFocusOnDismissal });

        this.mute.subscribe(() => {
            serverBuildReminder.mute(this.mute());
        });
    }

    detached() {
        super.detached();
        this.dialogTask.resolve();
    }

    close() {
        dialog.close(this);
    }
}

export = latestBuildReminder;
