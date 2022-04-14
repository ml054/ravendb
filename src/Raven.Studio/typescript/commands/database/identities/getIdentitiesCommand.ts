import commandBase from "commands/commandBase";
import endpoints from "endpoints";
import database from "models/resources/database";

class getIdentitiesCommand extends commandBase {

    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<dictionary<number>> {
        const url = endpoints.databases.identityDebug.debugIdentities;
        
        return this.query<dictionary<number>>(url, null, this.db)
            .fail((response: JQueryXHR) => this.reportError("Failed to get identities", response.responseText, response.statusText));
    }
}

export = getIdentitiesCommand;
