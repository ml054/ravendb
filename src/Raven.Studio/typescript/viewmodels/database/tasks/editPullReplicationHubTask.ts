import app = require("durandal/app");
import appUrl = require("common/appUrl");
import viewModelBase = require("viewmodels/viewModelBase");
import router = require("plugins/router");
import savePullReplicationHubTaskCommand = require("commands/database/tasks/savePullReplicationHubTaskCommand");
import pullReplicationDefinition = require("models/database/tasks/pullReplicationDefinition");
import eventsCollector = require("common/eventsCollector");
import getPossibleMentorsCommand = require("commands/database/tasks/getPossibleMentorsCommand");
import jsonUtil = require("common/jsonUtil");
import getPullReplicationHubTasksInfoCommand = require("commands/database/tasks/getPullReplicationHubTasksInfoCommand");
import pullReplicationGenerateCertificateCommand = require("commands/database/tasks/pullReplicationGenerateCertificateCommand");
import pullReplicationCertificate = require("models/database/tasks/pullReplicationCertificate");
import messagePublisher = require("common/messagePublisher");
import fileDownloader = require("common/fileDownloader");
import forge = require("node-forge");
import clusterTopologyManager = require("common/shell/clusterTopologyManager");
import generatePullReplicationCertificateConfirm = require("viewmodels/database/tasks/generatePullReplicationCertificateConfirm");
import fileImporter = require("common/fileImporter");

class editPullReplicationHubTask extends viewModelBase {

    editedItem = ko.observable<pullReplicationDefinition>();
    isAddingNewTask = ko.observable<boolean>(true);
    private taskId: number = null;
    
    canDefineCertificates = location.protocol === "https:";
    
    possibleMentors = ko.observableArray<string>([]);
    
    spinners = { 
        save: ko.observable<boolean>(false),
        generateCertificate: ko.observable<boolean>(false)
    };
    
    constructor() {
        super();
        
        this.bindToCurrentInstance("generateCertificate", "deleteCertificate", "certificateSelected", "downloadPfx");
    }

    activate(args: any) { 
        super.activate(args);
        const deferred = $.Deferred<void>();

        if (args.taskId) {
            // 1. Editing an existing task
            this.isAddingNewTask(false);
            this.taskId = args.taskId;

            new getPullReplicationHubTasksInfoCommand(this.activeDatabase(), this.taskId)
                .execute()
                .done((result: Raven.Client.Documents.Operations.Replication.PullReplicationDefinitionAndCurrentConnections) => { 
                    this.editedItem(new pullReplicationDefinition(result.Definition, this.canDefineCertificates));
                    deferred.resolve();
                })
                .fail(() => {
                    deferred.reject();
                    
                    router.navigate(appUrl.forOngoingTasks(this.activeDatabase()));
                });
        } else {
            // 2. Creating a new task
            this.isAddingNewTask(true);
            this.editedItem(pullReplicationDefinition.empty(this.canDefineCertificates));
            deferred.resolve();
        }

        deferred.done(() => this.initObservables());
        
        return $.when<any>(this.loadPossibleMentors(), deferred);
    }
    
    private loadPossibleMentors() {
        return new getPossibleMentorsCommand(this.activeDatabase().name)
            .execute()
            .done(mentors => this.possibleMentors(mentors));
    }

    private initObservables() {        
        const model = this.editedItem();
        
        this.dirtyFlag = new ko.DirtyFlag([
            model.taskName,
            model.manualChooseMentor,
            model.mentorNode,
            model.delayReplicationTime,
            model.showDelayReplication,
            model.certificates
        ], false, jsonUtil.newLineNormalizingHashFunction);
    }

    compositionComplete() {
        super.compositionComplete();
        document.getElementById('taskName').focus();
        
        $('.edit-pull-replication-hub-task [data-toggle="tooltip"]').tooltip(); 
    }

    savePullReplication() {
        if (!this.isValid(this.editedItem().validationGroup)) {
            return;
        }

        this.spinners.save(true);

        const dto = this.editedItem().toDto(this.taskId);
        this.taskId = this.isAddingNewTask() ? 0 : this.taskId;

        eventsCollector.default.reportEvent("pull-replication-hub", "save");

        new savePullReplicationHubTaskCommand(this.activeDatabase(), dto)
            .execute()
            .done(() => {
                this.dirtyFlag().reset();
                this.goToOngoingTasksView();
            })
            .always(() => this.spinners.save(false));
    }
   
    cancelOperation() {
        this.goToOngoingTasksView();
    }

    private goToOngoingTasksView() {
        router.navigate(appUrl.forOngoingTasks(this.activeDatabase()));
    }

    generateCertificate() {
        app.showBootstrapDialog(new generatePullReplicationCertificateConfirm())
            .done(validity => {
                if (validity != null) {
                    this.spinners.generateCertificate(true);

                    new pullReplicationGenerateCertificateCommand(this.activeDatabase(), validity)
                        .execute()
                        .done(result => {
                            this.editedItem().certificates.push(
                                new pullReplicationCertificate(result.PublicKey, result.Certificate));

                            // reset export status
                            this.editedItem().certificateExported(false);
                        })
                        .always(() => this.spinners.generateCertificate(false));
                }
            });
    }

    exportConfiguration() {
        if (!this.isValid(this.editedItem().exportValidationGroup)) {
            return;
        }
        
        const item = this.editedItem();
        const topologyUrls = clusterTopologyManager
            .default
            .topology()
            .nodes()
            .map(x => x.serverUrl());
        
        const configurationToExport = {
            Database: this.activeDatabase().name,
            HubDefinitionName: item.taskName(),
            Certificate: item.getCertificate(),
            TopologyUrls: topologyUrls,
        } as pullReplicationExportFileFormat;
        
        const fileName = "configFile-" + item.taskName() + ".json";
        
        this.editedItem().certificateExported(true);
        
        fileDownloader.downloadAsJson(configurationToExport, fileName);
    }

    certificateSelected(fileInput: HTMLInputElement) {
        fileImporter.readAsText(fileInput,data => this.certificateImported(data));
    }

    certificateImported(cert: string) {
        try {
            this.editedItem().certificates.push(new pullReplicationCertificate(cert));
        } catch ($e) {
            messagePublisher.reportError("Unable to import certificate", $e);
        }
    }

    downloadPfx() {
        const certificate = this.editedItem().certificates().find(x => !!x.certificate());
        
        if (certificate) {
            const pfx = forge.util.binary.base64.decode(certificate.certificate());
            
            const fileName = "pullReplication-" + certificate.thumbprint().substr(0, 8) + ".pfx";

            this.editedItem().certificateExported(true);
            
            fileDownloader.downloadAsTxt(pfx, fileName);
        }
    }

    deleteCertificate(certificate: pullReplicationCertificate) {
        this.editedItem().certificates.remove(certificate);
    }
}

export = editPullReplicationHubTask;
