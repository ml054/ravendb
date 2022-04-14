import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getSystemStorageReportCommand extends commandBase {

    execute(): JQueryPromise<detailedSystemStorageReportItemDto> {
        const args = {
            detailed: false
        };
        const url = endpoints.global.adminStorage.adminDebugStorageEnvironmentReport + this.urlEncodeArgs(args);
        return this.query<detailedSystemStorageReportItemDto>(url, null)
            .fail((response: JQueryXHR) => this.reportError("Failed to load storage report", response.responseText, response.statusText));
    }
}

export = getSystemStorageReportCommand;
