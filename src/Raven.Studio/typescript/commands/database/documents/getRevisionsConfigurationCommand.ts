import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getRevisionsConfigurationCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Operations.Revisions.RevisionsConfiguration> {

        const deferred = $.Deferred<Raven.Client.Documents.Operations.Revisions.RevisionsConfiguration>();
        const url = endpoints.databases.revisions.revisionsConfig;
        this.query(url, null, this.db)
            .done((revisionsConfig: Raven.Client.Documents.Operations.Revisions.RevisionsConfiguration) => deferred.resolve(revisionsConfig))
            .fail((xhr: JQueryXHR) => {
                if (xhr.status === 404) {
                    deferred.resolve(null);
                } else {
                    deferred.reject(xhr);
                    this.reportError("Failed to get revisions information", xhr.responseText, xhr.statusText);
                }
                
            });

        return deferred;
    }
}

export = getRevisionsConfigurationCommand;
