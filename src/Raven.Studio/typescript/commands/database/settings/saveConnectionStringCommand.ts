import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");
import connectionStringRavenEtlModel = require("models/database/settings/connectionStringRavenEtlModel");
import connectionStringSqlEtlModel = require("models/database/settings/connectionStringSqlEtlModel");

class saveConnectionStringCommand extends commandBase {

    constructor(private db: database, private connectionString: connectionStringRavenEtlModel | connectionStringSqlEtlModel) {
        super();
    }
 
    execute(): JQueryPromise<void> { 
        return this.saveConnectionString()
            .fail((response: JQueryXHR) => this.reportError("Failed to save connection string", response.responseText, response.statusText))
            .done(() => this.reportSuccess(`Connection string was saved successfully`));
    }

    private saveConnectionString(): JQueryPromise<void> { 
        
        const args = { name: this.db.name };
        const url = endpoints.global.adminDatabases.adminConnectionStrings + this.urlEncodeArgs(args);
        
        const saveConnectionStringTask = $.Deferred<void>();
        
        const payload = this.connectionString.toDto();

        this.put(url, JSON.stringify(payload))
            .done(() => saveConnectionStringTask.resolve())
            .fail(response => saveConnectionStringTask.reject(response));

        return saveConnectionStringTask;
    }
}

export = saveConnectionStringCommand; 

