import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class claimDomainCommand extends commandBase {

    constructor(private domain: string, private license: Raven.Server.Commercial.License) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            action: "claim"
        };
        const url = endpoints.global.setup.setupDnsNCert + this.urlEncodeArgs(args); 
        const payload: Raven.Server.Commercial.ClaimDomainInfo = { 
            Domain: this.domain,
            License: this.license
        };

        return this.post(url, JSON.stringify(payload), null, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError("Failed to obtain domain information", response.responseText, response.statusText));
    }
}

export = claimDomainCommand;
