import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class testOlapEtlCommand extends commandBase {
    constructor(private db: database, private payload: Raven.Server.Documents.ETL.Providers.OLAP.Test.TestOlapEtlScript) {
        super();
    }  

    execute(): JQueryPromise<Raven.Server.Documents.ETL.Providers.OLAP.Test.OlapEtlTestScriptResult> {
        const url = endpoints.databases.olapEtl.adminEtlOlapTest;

        return this.post<Raven.Server.Documents.ETL.Providers.OLAP.Test.OlapEtlTestScriptResult>(url, JSON.stringify(this.payload), this.db)
            .fail((response: JQueryXHR) => {
                this.reportError(`Failed to test OLAP ETL`, response.responseText, response.statusText);
            });
    }
}

export = testOlapEtlCommand; 

