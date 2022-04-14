import eventsCollector from "common/eventsCollector";
import appUrl from "common/appUrl";
import router from "plugins/router";
import dialogViewModelBase from "viewmodels/dialogViewModelBase"; 

class createServerWideTask extends dialogViewModelBase {

    view = require("views/manage/createServerWideTask.html");

    newServerWideReplicationTask() {
        eventsCollector.default.reportEvent("serverWideExternalReplication", "new");
        const url = appUrl.forEditServerWideExternalReplication();
        router.navigate(url);
        this.close();
    }

    newServerWideBackupTask() {
        eventsCollector.default.reportEvent("serverWidePeriodicBackup", "new");
        const url = appUrl.forEditServerWideBackup();
        router.navigate(url);
        this.close();
    }
}

export = createServerWideTask;
