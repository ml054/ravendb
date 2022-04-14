import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class testElasticSearchEtlCommand extends commandBase {
    constructor(private db: database, private payload: Raven.Server.Documents.ETL.Providers.ElasticSearch.Test.TestElasticSearchEtlScript) {
        super();
    }  

    execute(): JQueryPromise<Raven.Server.Documents.ETL.Providers.ElasticSearch.Test.ElasticSearchEtlTestScriptResult> {
        const url = endpoints.databases.elasticSearchEtl.adminEtlElasticsearchTest;

        return this.post<Raven.Server.Documents.ETL.Providers.ElasticSearch.Test.ElasticSearchEtlTestScriptResult>(url, JSON.stringify(this.payload), this.db)
            .fail((response: JQueryXHR) => {
                this.reportError(`Failed to test Elasticsearch ETL`, response.responseText, response.statusText);
            });
    }
}

export = testElasticSearchEtlCommand;
