import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class saveUnsecuredSetupCommand extends commandBase {

    constructor(private dto: Raven.Server.Commercial.UnsecuredSetupInfo) {
        super();
    }

    execute(): JQueryPromise<void> {
        const url = endpoints.global.setup.setupUnsecured;

        return this.post(url, JSON.stringify(this.dto), null, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError("Failed to save configuration", response.responseText, response.statusText));
    }
}

export = saveUnsecuredSetupCommand;
