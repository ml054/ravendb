/// <reference path="../../../../typings/tsd.d.ts"/>
import database = require("models/resources/database");
import connectionStringModel = require("models/database/settings/connectionStringModel");
import saveConnectionStringCommand_OLD = require("commands/database/settings/saveConnectionStringCommand_OLD");
import jsonUtil = require("common/jsonUtil");
import testAzureQueueStorageServerConnectionCommand
    from "commands/database/cluster/testAzureQueueStorageServerConnectionCommand";
import assertUnreachable from "components/utils/assertUnreachable";

class AzureQueueStorageConnectionStringModel {
    connectionString = ko.observable<string>();
    
    onChange(action: () => void) {
        this.connectionString.subscribe(action);
    }
    
    toDto(): Raven.Client.Documents.Operations.ETL.Queue.Authentication {
        return {
            ConnectionString: this.connectionString(),
            EntraId: null,
            Passwordless: false
        }
    }
}

class AzureQueueStorageEntraIdModel {
    clientId = ko.observable<string>();
    clientSecret = ko.observable<string>();
    storageAccountName = ko.observable<string>();
    tenantId = ko.observable<string>();

    onChange(action: () => void) {
        this.clientId.subscribe(action);
        this.clientSecret.subscribe(action);
        this.storageAccountName.subscribe(action);
        this.tenantId.subscribe(action);
    }
    
    toDto(): Raven.Client.Documents.Operations.ETL.Queue.Authentication {
        return {
            ConnectionString: null,
            EntraId: {
                ClientId: this.clientId(),
                ClientSecret: this.clientSecret(),
                StorageAccountName: this.storageAccountName(),
                TenantId: this.tenantId(),
            },
            Passwordless: false
        }
    }

    update(dto: Raven.Client.Documents.Operations.ETL.Queue.EntraId) {
        this.clientId(dto.ClientId);
        this.clientSecret(dto.ClientSecret);
        this.storageAccountName(dto.StorageAccountName);
        this.tenantId(dto.TenantId);
    }
}

class AzureQueueStoragePasswordlessModel {
    
    onChange(action: () => void) {
        // empty by design
    }
    
    toDto(): Raven.Client.Documents.Operations.ETL.Queue.Authentication {
        return {
            ConnectionString: null,
            EntraId: null,
            Passwordless: true
        }
    }
}

class connectionStringAzureQueueStorageModel extends connectionStringModel {

    authenticationType = ko.observable<AzureQueueStorageAuthenticationType>("connectionString");
    
    connectionStringConfiguration = new AzureQueueStorageConnectionStringModel();
    entraIdModel = new AzureQueueStorageEntraIdModel();
    passwordlessModel = new AzureQueueStoragePasswordlessModel();

    validationGroup: KnockoutValidationGroup;
    dirtyFlag: () => DirtyFlag;

    constructor(dto: Raven.Client.Documents.Operations.ETL.Queue.QueueConnectionString, isNew: boolean, tasks: { taskName: string; taskId: number }[]) {
        super(isNew, tasks);

        this.update(dto);
        this.initValidation();

        this.dirtyFlag = new ko.DirtyFlag([
            this.connectionStringName,
            this.authenticationType,
            this.connectionStringConfiguration.connectionString,
            this.entraIdModel.tenantId,
            this.entraIdModel.storageAccountName,
            this.entraIdModel.clientSecret,
            this.entraIdModel.clientId,
        ], false, jsonUtil.newLineNormalizingHashFunction);
    }
    
    onChange(action: () => void) {
        this.authenticationType.subscribe(action);
        this.connectionStringConfiguration.onChange(action);
        this.entraIdModel.onChange(action);
        this.passwordlessModel.onChange(action);
    }

    update(dto: Raven.Client.Documents.Operations.ETL.Queue.QueueConnectionString) {
        super.update(dto);

        const settings = dto.AzureQueueStorageConnectionSettings;
        if (settings.Authentication.Passwordless) {
            this.authenticationType("passwordless");
        } else if (settings.Authentication.ConnectionString) {
            this.authenticationType("connectionString");
            this.connectionStringConfiguration.connectionString(settings.Authentication.ConnectionString);
        } else if (settings.Authentication.EntraId) {
            this.authenticationType("entraId");
            this.entraIdModel.update(settings.Authentication.EntraId);
        }
    }

    initValidation() {
        super.initValidation();

        this.validationGroup = ko.validatedObservable({
            connectionStringName: this.connectionStringName,
            connectionStringConfigurationConnectionString: this.connectionStringConfiguration.connectionString,
            entraIdClientId: this.entraIdModel.clientId,
            entraIdClientSecret: this.entraIdModel.clientSecret,
            entraIdStorageAccountName: this.entraIdModel.storageAccountName,
            entraIdTenantId: this.entraIdModel.tenantId,
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
                    EntraId: null,
                    Passwordless: false,
                }
            },
        }, true, []);
    }
    
    private authenticationToDto(): Raven.Client.Documents.Operations.ETL.Queue.Authentication {
        const authenticationType = this.authenticationType();
        switch (authenticationType) {
            case "connectionString": 
                return this.connectionStringConfiguration.toDto();
            case "entraId":
                return this.entraIdModel.toDto();
            case "passwordless":
                return this.passwordlessModel.toDto();
            default:
                assertUnreachable(authenticationType);
        }
    }
    
    toDto(): Raven.Client.Documents.Operations.ETL.Queue.QueueConnectionString  {
        return {
            Type: "Queue",
            BrokerType: "AzureQueueStorage",
            Name: this.connectionStringName(),
            RabbitMqConnectionSettings: null,
            KafkaConnectionSettings: null,
            AzureQueueStorageConnectionSettings: {
                Authentication: this.authenticationToDto()
            },
        };
    }

    saveConnectionString(db: database) : JQueryPromise<void> {
        return new saveConnectionStringCommand_OLD(db, this)
            .execute();
    }

    testConnection(db: database): JQueryPromise<Raven.Server.Web.System.NodeConnectionTestResult> {
        return new testAzureQueueStorageServerConnectionCommand(db, this.authenticationToDto())
            .execute();
    }
}

export = connectionStringAzureQueueStorageModel;
