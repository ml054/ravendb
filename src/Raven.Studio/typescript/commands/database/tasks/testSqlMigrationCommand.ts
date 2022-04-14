import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class testSqlMigrationCommand extends commandBase {
    
    constructor(private db: database, private dto: Raven.Server.SqlMigration.Model.MigrationTestRequest) {
          super();
    }

    execute(): JQueryPromise<{ DocumentId: string,  Document: any }> {
        const url = endpoints.databases.sqlMigration.adminSqlMigrationTest;
        return this.post<operationIdDto>(url, JSON.stringify(this.dto), this.db)
            .fail((response: JQueryXHR) => {
                this.reportError(`Failed to perform test`, response.responseText, response.statusText);
            });
    }
}

export = testSqlMigrationCommand; 
