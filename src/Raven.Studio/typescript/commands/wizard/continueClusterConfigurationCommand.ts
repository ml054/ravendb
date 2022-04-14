import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class continueClusterConfigurationCommand extends commandBase {

    constructor(private operationId: number, private dto: Raven.Server.Commercial.ContinueSetupInfo) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            operationId: this.operationId
        }
        const url = endpoints.global.setup.setupContinue + this.urlEncodeArgs(args);

        return this.post<operationIdDto>(url, JSON.stringify(this.dto), null, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError("Failed to configure cluster node", response.responseText, response.statusText));
    }
}

export = continueClusterConfigurationCommand;
