import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class saveDatabaseSettingsCommand extends commandBase {
   
    constructor(private db: database, private settingsToSave: Record<string, string>) {
        super();
    }
    
    execute(): JQueryPromise<void> {
        
        const url = endpoints.global.adminConfiguration.adminConfigurationSettings;
        
        return this.put<void>(url, JSON.stringify(this.settingsToSave), this.db, { dataType: undefined })
            .done(() => this.reportSuccess("Database Settings were saved successfully"))
            .fail((response: JQueryXHR) => this.reportError("Failed to save Database Settings", response.responseText, response.statusText));
    }
}

export = saveDatabaseSettingsCommand;
