import eventsCollector from "common/eventsCollector";
import appUrl from "common/appUrl";
import router from "plugins/router";
import dialogViewModelBase from "viewmodels/dialogViewModelBase"; 

class createOngoingTask extends dialogViewModelBase {

    view = require("views/database/tasks/createOngoingTask.html");

    newReplicationTask() {
        eventsCollector.default.reportEvent("ExternalReplication", "new");
        const url = appUrl.forEditExternalReplication(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newBackupTask() {
        eventsCollector.default.reportEvent("PeriodicBackup", "new");
        const url = appUrl.forEditPeriodicBackupTask(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newSubscriptionTask() {
        eventsCollector.default.reportEvent("Subscription", "new");
        const url = appUrl.forEditSubscription(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newRavenEtlTask() {
        eventsCollector.default.reportEvent("RavenETL", "new");
        const url = appUrl.forEditRavenEtl(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newSqlEtlTask() {
        eventsCollector.default.reportEvent("SqlETL", "new");
        const url = appUrl.forEditSqlEtl(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newOlapEtlTask() {
        eventsCollector.default.reportEvent("OlapETL", "new");
        const url = appUrl.forEditOlapEtl(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newElasticSearchEtlTask() {
        eventsCollector.default.reportEvent("ElasticSearchETL", "new");
        const url = appUrl.forEditElasticSearchEtl(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newReplicationHubTask() {
        eventsCollector.default.reportEvent("ReplicationHub", "new");
        const url = appUrl.forEditReplicationHub(this.activeDatabase());
        router.navigate(url);
        this.close();
    }

    newReplicationSinkTask() {
        eventsCollector.default.reportEvent("ReplicationSink", "new");
        const url = appUrl.forEditReplicationSink(this.activeDatabase());
        router.navigate(url);
        this.close();
    }
}

export = createOngoingTask;
