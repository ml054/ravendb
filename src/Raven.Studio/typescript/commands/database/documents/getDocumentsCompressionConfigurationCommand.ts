import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getDocumentsCompressionConfigurationCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<Raven.Client.ServerWide.DocumentsCompressionConfiguration> {
        const url = endpoints.databases.documentsCompression.documentsCompressionConfig;
        const getConfigurationTask = $.Deferred<Raven.Client.ServerWide.DocumentsCompressionConfiguration>();

        this.query<Raven.Client.ServerWide.DocumentsCompressionConfiguration>(url, null, this.db)
            .done(dto => getConfigurationTask.resolve(dto)) 
            .fail((response: JQueryXHR) => {
                if (response.status !== 404) {
                    this.reportError(`Failed to get documents compression configuration`, response.responseText, response.statusText);
                    getConfigurationTask.reject(response);
                } else {
                    getConfigurationTask.resolve(null);
                }
            });

        return getConfigurationTask;
    }
}

export = getDocumentsCompressionConfigurationCommand;
