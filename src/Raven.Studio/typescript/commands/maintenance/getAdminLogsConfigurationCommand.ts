import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getAdminLogsConfigurationCommand extends commandBase {
    
    execute(): JQueryPromise<adminLogsConfiguration> {
        const url = endpoints.global.adminLogs.adminLogsConfiguration;
        
        return this.query<adminLogsConfiguration>(url, null)
            .fail((response: JQueryXHR) => this.reportError(`Failed to get admin logs configuration`, response.responseText, response.statusText)) 
    }
}

export = getAdminLogsConfigurationCommand;
