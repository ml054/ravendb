﻿/// <reference path="../../../../typings/tsd.d.ts"/>
import ongoingTaskEditModel = require("models/database/tasks/ongoingTaskEditModel");
import getRevisionsConfigurationCommand = require("commands/database/documents/getRevisionsConfigurationCommand");
import activeDatabaseTracker = require("common/shell/activeDatabaseTracker");
import collectionsTracker = require("common/helpers/database/collectionsTracker");

class ongoingTaskSubscriptionEditModel extends ongoingTaskEditModel {

    liveConnection = ko.observable<boolean>();

    query = ko.observable<string>();

    startingPointType = ko.observable<subscriptionStartType>();
    startingChangeVector = ko.observable<string>();
    startingPointChangeVector: KnockoutComputed<boolean>;
    startingPointLatestDocument: KnockoutComputed<boolean>; 
    setStartingPoint = ko.observable<boolean>(true);
    
    changeVectorForNextBatchStartingPoint = ko.observable<string>(null); 

    validationGroup: KnockoutValidationGroup; 

    constructor(dto: Raven.Client.Documents.Subscriptions.SubscriptionStateWithNodeDetails) {
        super();
        
        this.query(dto.Query);
        this.updateDetails(dto);
        this.initializeObservables(); 
        this.initValidation();
    }

    initializeObservables() {
        super.initializeObservables();
        
        this.startingPointType("Beginning of Time");

        this.startingPointChangeVector = ko.pureComputed(() => {
            return this.startingPointType() === "Change Vector";
        });

        this.startingPointLatestDocument = ko.pureComputed(() => {
            return this.startingPointType() === "Latest Document";
        });
    }   

    updateDetails(dto: Raven.Client.Documents.Subscriptions.SubscriptionStateWithNodeDetails) {
        const dtoEditModel = dto as Raven.Client.Documents.Subscriptions.SubscriptionStateWithNodeDetails;

        const state: Raven.Client.ServerWide.Operations.OngoingTaskState = dtoEditModel.Disabled ? 'Disabled' : 'Enabled';
        const emptyNodeId: Raven.Client.ServerWide.Operations.NodeId = { NodeTag: "", NodeUrl: "", ResponsibleNode: "" };

        const dtoListModel: Raven.Client.ServerWide.Operations.OngoingTask = {
            ResponsibleNode: emptyNodeId,
            TaskConnectionStatus: 'Active',
            TaskId: dtoEditModel.SubscriptionId,
            TaskName: dtoEditModel.SubscriptionName,
            TaskState: state,
            TaskType: 'Subscription',
            Error: null
        };

        super.update(dtoListModel);
        
        this.manualChooseMentor(!!dto.MentorNode);
        this.preferredMentor(dto.MentorNode);

        this.query(dto.Query);
        this.changeVectorForNextBatchStartingPoint(dto.ChangeVectorForNextBatchStartingPoint);
        this.setStartingPoint(false);
    }

    private serializeChangeVector() {
        let changeVector: Raven.Client.Constants.Documents.SubscriptionChangeVectorSpecialStates | string = this.taskId ? "DoNotChange" : "BeginningOfTime"; 

        if (this.setStartingPoint()) {
            switch (this.startingPointType()) {
                case "Beginning of Time":
                    changeVector = "BeginningOfTime";
                    break;
                case "Latest Document":
                    changeVector = "LastDocument";
                    break;
                case "Change Vector":
                    changeVector = this.startingChangeVector();
                    break;
            }
        }
        return changeVector;
    }
    
    toTestDto() {
        const subscriptionToTest: Raven.Client.Documents.Subscriptions.SubscriptionTryout = {
            ChangeVector: this.serializeChangeVector(),
            Query: this.query()
        };
    }
    
    toDto(): Raven.Client.Documents.Subscriptions.SubscriptionCreationOptions {
        const query = _.trim(this.query()) || null;

        return {
            Name: this.taskName(),
            Query: query,
            MentorNode: this.manualChooseMentor() ? this.preferredMentor() : undefined,
            ChangeVector: this.serializeChangeVector()
        }
    }

    initValidation() {
        this.query.extend({
            required: true,
            aceValidation: true
        });
        
        this.initializeMentorValidation();

        this.startingChangeVector.extend({
            validation: [
                {
                    validator: () => {
                        const goodState1 = this.setStartingPoint() && this.startingPointType() === 'Change Vector' && this.startingChangeVector();
                        const goodState2 = this.setStartingPoint() && this.startingPointType() !== 'Change Vector';
                        const goodState3 = !this.setStartingPoint();
                        return goodState1 || goodState2 || goodState3;
                    },
                    message: "Please enter change vector"
                }]
        });

        this.validationGroup = ko.validatedObservable({
            query: this.query,
            startingChangeVector: this.startingChangeVector,
            preferredMentor: this.preferredMentor
            
        });
    }

    static empty(): ongoingTaskSubscriptionEditModel {
        return new ongoingTaskSubscriptionEditModel(
            {
                Disabled: false,
                Query: "",
                ChangeVectorForNextBatchStartingPoint: null,
                SubscriptionId: 0,
                SubscriptionName: null,
                ResponsibleNode: null,
                LastClientConnectionTime: null,
                LastTimeServerMadeProgressWithDocuments: null,
                MentorNode: null
            });
    }
}

export = ongoingTaskSubscriptionEditModel;
