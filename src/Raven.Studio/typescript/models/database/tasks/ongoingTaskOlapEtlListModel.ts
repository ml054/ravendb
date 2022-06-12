﻿/// <reference path="../../../../typings/tsd.d.ts"/>
import activeDatabaseTracker = require("common/shell/activeDatabaseTracker");
import abstractOngoingTaskEtlListModel = require("models/database/tasks/abstractOngoingTaskEtlListModel");
import appUrl = require("common/appUrl");

class ongoingTaskOlapEtlListModel extends abstractOngoingTaskEtlListModel {
    
    destinationDescription = ko.observable<string>();
    
    destinations = ko.observableArray<string>();
    
    connectionStringDefined = ko.observable<boolean>(true); // needed for template in the ongoing tasks list view

    get studioTaskType(): StudioTaskType {
        return "OlapEtl";
    }
    
    constructor(dto: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskOlapEtlListView) {
        super();

        this.update(dto);
        this.initializeObservables();

        this.connectionStringsUrl = appUrl.forConnectionStrings(activeDatabaseTracker.default.database(), "Olap", this.connectionStringName());
    }

    initializeObservables() {
        super.initializeObservables();

        const urls = appUrl.forCurrentDatabase();
        this.editUrl = urls.editOlapEtl(this.taskId);
        
        this.destinations(this.destinationDescription().split(','));
    }

    update(dto: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskOlapEtlListView) {
        super.update(dto);

        this.connectionStringName(dto.ConnectionStringName);
        this.destinationDescription(dto.Destination);
    }
}

export = ongoingTaskOlapEtlListModel;
