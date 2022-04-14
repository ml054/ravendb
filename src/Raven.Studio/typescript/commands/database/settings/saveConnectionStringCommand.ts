import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";
import connectionStringRavenEtlModel from "models/database/settings/connectionStringRavenEtlModel";
import connectionStringSqlEtlModel from "models/database/settings/connectionStringSqlEtlModel";
import connectionStringOlapEtlModel from "models/database/settings/connectionStringOlapEtlModel";
import connectionStringElasticSearchEtlModel from "models/database/settings/connectionStringElasticSearchEtlModel";

class saveConnectionStringCommand extends commandBase {

    constructor(private db: database, private connectionString: connectionStringRavenEtlModel |
                                                                connectionStringSqlEtlModel   |
                                                                connectionStringOlapEtlModel  |
                                                                connectionStringElasticSearchEtlModel) {
        super();
    }
 
    execute(): JQueryPromise<void> { 
        return this.saveConnectionString()
            .fail((response: JQueryXHR) => this.reportError("Failed to save connection string", response.responseText, response.statusText))
            .done(() => this.reportSuccess(`Connection string was saved successfully`));
    }

    private saveConnectionString(): JQueryPromise<void> { 
        
        const url = endpoints.databases.ongoingTasks.adminConnectionStrings;
        
        const saveConnectionStringTask = $.Deferred<void>();
        
        const payload = this.connectionString.toDto();

        this.put(url, JSON.stringify(payload), this.db)
            .done(() => saveConnectionStringTask.resolve())
            .fail(response => saveConnectionStringTask.reject(response));

        return saveConnectionStringTask;
    }
}

export = saveConnectionStringCommand; 

