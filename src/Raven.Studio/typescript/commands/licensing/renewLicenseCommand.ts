import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class renewLicenseCommand extends commandBase {

    execute(): JQueryPromise<Raven.Server.Commercial.LicenseRenewalResult> {
        const url = endpoints.global.license.adminLicenseRenew;
        
        return this.post(url, null, null)
            .fail((response: JQueryXHR) => {
                this.reportError("Failed to renew license", response.responseText, response.statusText);
            });
    }
}

export = renewLicenseCommand;
