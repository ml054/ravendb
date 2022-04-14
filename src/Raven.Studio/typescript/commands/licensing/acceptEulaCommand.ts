import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class acceptEulaCommand extends commandBase {

    execute(): JQueryPromise<void> {
        const url = endpoints.global.license.adminLicenseEulaAccept;
        return this.post<void>(url, null, null, { dataType: undefined })
            .fail((response: JQueryXHR) => this.reportError("Failed to save license settings", response.responseText));
    }
}

export = acceptEulaCommand;
