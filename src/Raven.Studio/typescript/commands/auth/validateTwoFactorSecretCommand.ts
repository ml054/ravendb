import commandBase = require("commands/commandBase");
import endpoints = require("endpoints");

class validateTwoFactorSecretCommand extends commandBase {

    constructor(private secret: string) {
        super();
    }
    
    execute(): JQueryPromise<void> {
        const url = endpoints.global.twoFactorAuthentication.authentication2fa;
        
        const payload = {
            Token: this.secret
        }
        
        return this.post<{ Secret: string }>(url, JSON.stringify(payload), null)
            .fail((response: JQueryXHR) => this.reportError("Unable to authenticate with 2FA", response.responseText, response.statusText));
    }
}

export = validateTwoFactorSecretCommand;
