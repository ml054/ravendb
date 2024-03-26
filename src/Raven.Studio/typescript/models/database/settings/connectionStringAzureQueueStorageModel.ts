/// <reference path="../../../../typings/tsd.d.ts"/>
import database = require("models/resources/database");
import connectionStringModel = require("models/database/settings/connectionStringModel");
import saveConnectionStringCommand_OLD = require("commands/database/settings/saveConnectionStringCommand_OLD");
import jsonUtil = require("common/jsonUtil");
import testAzureQueueStorageServerConnectionCommand
    from "commands/database/cluster/testAzureQueueStorageServerConnectionCommand";

class connectionStringAzureQueueStorageModel extends connectionStringModel {

    azureQueueStorageConnectionString = ko.observable<string>();

    validationGroup: KnockoutValidationGroup;
    dirtyFlag: () => DirtyFlag;

    constructor(dto: Raven.Client.Documents.Operations.ETL.Queue.QueueConnectionString, isNew: boolean, tasks: { taskName: string; taskId: number }[]) {
        super(isNew, tasks);

        this.update(dto);
        this.initValidation();

        this.dirtyFlag = new ko.DirtyFlag([
            this.connectionStringName,
            this.azureQueueStorageConnectionString
        ], false, jsonUtil.newLineNormalizingHashFunction);
    }

    update(dto: Raven.Client.Documents.Operations.ETL.Queue.QueueConnectionString) {
        super.update(dto);

        const settings = dto.AzureQueueStorageConnectionSettings;
        this.azureQueueStorageConnectionString(settings.Authentication.ConnectionString); //TODO: entra id
    }

    initValidation() {
        super.initValidation();

        this.azureQueueStorageConnectionString.extend({
            required: true
        });

        this.validationGroup = ko.validatedObservable({
            connectionStringName: this.connectionStringName,
            azureQueueStorageConnectionString: this.azureQueueStorageConnectionString,
        });
    }

    static empty(): connectionStringAzureQueueStorageModel {
        return new connectionStringAzureQueueStorageModel({
            Type: "Queue",
            BrokerType: "AzureQueueStorage",
            Name: "",

            RabbitMqConnectionSettings: null,
            KafkaConnectionSettings: null,
            AzureQueueStorageConnectionSettings: {
                Authentication: {
                    ConnectionString: "",
                    EntraId: null
                }
            },
        }, true, []);
    }

    toDto(): Raven.Client.Documents.Operations.ETL.Queue.QueueConnectionString  {
        return {
            Type: "Queue",
            BrokerType: "AzureQueueStorage",
            Name: this.connectionStringName(),

            RabbitMqConnectionSettings: null,
            KafkaConnectionSettings: null,
            AzureQueueStorageConnectionSettings: {
                Authentication: {
                    ConnectionString: this.azureQueueStorageConnectionString(),
                    EntraId: null,
                }
            },
        };
    }

    saveConnectionString(db: database) : JQueryPromise<void> {
        return new saveConnectionStringCommand_OLD(db, this)
            .execute();
    }

    testConnection(db: database): JQueryPromise<Raven.Server.Web.System.NodeConnectionTestResult> {
        return new testAzureQueueStorageServerConnectionCommand(db, this.azureQueueStorageConnectionString())
            .execute();
    }
}

export = connectionStringAzureQueueStorageModel;
