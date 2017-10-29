import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class getConnectionStringInfoCommand extends commandBase {
    private constructor(private db: database, private type: Raven.Client.ServerWide.ConnectionStringType, private connectionStringName: string) {
        super();
    }
    
    execute(): JQueryPromise<Raven.Client.ServerWide.Operations.ConnectionStrings.GetConnectionStringsResult> {
        return this.getConnectionStringInfo()
            .fail((response: JQueryXHR) => {
                this.reportError(`Failed to get info for connection string: ${this.connectionStringName}`, response.responseText, response.statusText);
            });
    }

    private getConnectionStringInfo(): JQueryPromise<Raven.Client.ServerWide.Operations.ConnectionStrings.GetConnectionStringsResult> {
        const args = { name: this.db.name, connectionStringName: this.connectionStringName, type: this.type };
        const url = endpoints.global.adminDatabases.adminConnectionStrings + this.urlEncodeArgs(args);

        return this.query(url, null);
    }

    static forRavenEtl(db: database, connectionStringName: string) {
        return new getConnectionStringInfoCommand(db, "Raven", connectionStringName);
    }

    static forSqlEtl(db: database, connectionStringName: string) {
        return new getConnectionStringInfoCommand(db, "Sql", connectionStringName);
    }
}

export = getConnectionStringInfoCommand; 
