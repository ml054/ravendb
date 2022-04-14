import viewModelBase from "viewmodels/viewModelBase";
import eventsCollector from "common/eventsCollector";
import generalUtils from "common/generalUtils";
import postgreSqlCredentialsModel from "models/database/settings/postgreSqlCredentialsModel";
import getIntegrationsPostgreSqlCredentialsCommand from "commands/database/settings/getIntegrationsPostgreSqlCredentialsCommand";
import getIntegrationsPostgreSqlSupportCommand from "commands/database/settings/getIntegrationsPostgreSqlSupportCommand";
import saveIntegrationsPostgreSqlCredentialsCommand from "commands/database/settings/saveIntegrationsPostgreSqlCredentialsCommand";
import deleteIntegrationsPostgreSqlCredentialsCommand from "commands/database/settings/deleteIntegrationsPostgreSqlCredentialsCommand";
import shardViewModelBase from "viewmodels/shardViewModelBase";
import database from "models/resources/database";

class integrations extends shardViewModelBase {

    view = require("views/database/settings/integrations.html");
    
    postgreSqlCredentials = ko.observableArray<string>([]);
    
    editedPostgreSqlCredentials = ko.observable<postgreSqlCredentialsModel>(null);

    //TODO
    testConnectionResult = ko.observable<Raven.Server.Web.System.NodeConnectionTestResult>();
    errorText: KnockoutComputed<string>;

    clientVersion = viewModelBase.clientVersion;
    isPostgreSqlSupportEnabled = ko.observable<boolean>();
    
    spinners = {
        test: ko.observable<boolean>(false)
    };

    constructor(db: database) {
        super(db);
        
        this.bindToCurrentInstance("onConfirmDelete");
        this.initObservables();
    }
    
    private initObservables(): void {
        this.errorText = ko.pureComputed(() => {
            const result = this.testConnectionResult();
            if (!result || result.Success) {
                return "";
            }
            return generalUtils.trimMessage(result.Error);
        });
    }

    activate(args: any) {
        super.activate(args);
        return $.when<any>(this.getAllIntegrationsCredentials(), this.getPostgreSqlSupportStatus());
    }
    
    private getPostgreSqlSupportStatus(): JQueryPromise<Raven.Server.Integrations.PostgreSQL.Handlers.PostgreSqlServerStatus> {
        return new getIntegrationsPostgreSqlSupportCommand(this.db)
            .execute()
            .done(result => this.isPostgreSqlSupportEnabled(result.Active))
    }
    
    private getAllIntegrationsCredentials(): JQueryPromise<Raven.Server.Integrations.PostgreSQL.Handlers.PostgreSqlUsernames> {
        return new getIntegrationsPostgreSqlCredentialsCommand(this.db)
            .execute()
            .done((result: Raven.Server.Integrations.PostgreSQL.Handlers.PostgreSqlUsernames) => {
                const users = result.Users.map(x => x.Username);
                this.postgreSqlCredentials(_.sortBy(users, userName => userName.toLowerCase()));
            });
    }
    
    onConfirmDelete(username: string): void {
        this.confirmationMessage("Delete credentials?",
            `You're deleting PostgreSQL credentials for user: <br><ul><li><strong>${generalUtils.escapeHtml(username)}</strong></li></ul>`, {
                buttons: ["Cancel", "Delete"],
                html: true
            })
            .done(result => {
                if (result.can) {
                    this.deleteIntegrationCredentials(username);
                }
            });
    }

    private deleteIntegrationCredentials(username: string): void {
        new deleteIntegrationsPostgreSqlCredentialsCommand(this.db, username)
            .execute()
            .done(() => {
                this.getAllIntegrationsCredentials();
                this.onCloseEdit();
            });
    }

    onAddPostgreSqlCredentials(): void {
        eventsCollector.default.reportEvent("PostgreSQL credentials", "add-postgreSql-credentials");
        
        this.editedPostgreSqlCredentials(new postgreSqlCredentialsModel(() => this.clearTestResult()));
        this.clearTestResult();
    }

    onSavePostgreSqlCredentials(): void {
        const modelToSave = this.editedPostgreSqlCredentials();
        if (modelToSave) {
            if (!this.isValid(modelToSave.validationGroup)) {
                return;
            }
            
            new saveIntegrationsPostgreSqlCredentialsCommand(this.db, modelToSave.username(), modelToSave.password())
                .execute()
                .done(() => {
                    this.getAllIntegrationsCredentials();
                    this.editedPostgreSqlCredentials(null);
                });
        }
    }

    onTestPostgreSqlCredentials(): void {
        this.clearTestResult();
        const postgreSqlCredentials = this.editedPostgreSqlCredentials();

        if (postgreSqlCredentials) {
            if (this.isValid(postgreSqlCredentials.validationGroup)) {
                eventsCollector.default.reportEvent("PostgreSQL credentials", "test-connection");

                // TODO
                // this.spinners.test(true);
                // postgreSqlCredentials.testConnection(this.db)
                //     .done((testResult) => this.testConnectionResult(testResult))
                //     .always(() => {
                //         this.spinners.test(false);
                //     });
            }
        }
    }

    onCloseEdit(): void {
        this.editedPostgreSqlCredentials(null);
    }

    private clearTestResult(): void {
        this.testConnectionResult(null);
    }
}

export = integrations
