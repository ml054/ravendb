import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getRevisionsForConflictsConfigurationCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.Documents.Operations.Revisions.RevisionsCollectionConfiguration> {
        const deferred = $.Deferred<Raven.Client.Documents.Operations.Revisions.RevisionsCollectionConfiguration>();
        const url = endpoints.databases.revisions.revisionsConflictsConfig;
        this.query(url, null, this.db)
            .done((revisionsConfig: Raven.Client.Documents.Operations.Revisions.RevisionsCollectionConfiguration) => deferred.resolve(revisionsConfig))
            .fail((xhr: JQueryXHR) => {
                if (xhr.status === 404) {
                    deferred.resolve(null);
                } else {
                    deferred.reject(xhr);
                    this.reportError("Failed to get revisions for conflicts information", xhr.responseText, xhr.statusText);
                }
                
            });

        return deferred;
    }
}

export = getRevisionsForConflictsConfigurationCommand;
