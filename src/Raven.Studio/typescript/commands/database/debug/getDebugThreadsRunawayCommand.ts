import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getDebugThreadsRunawayCommand extends commandBase {

    execute(): JQueryPromise<Raven.Server.Dashboard.ThreadInfo[]> {
        const url = endpoints.global.threads.adminDebugThreadsRunaway;
        return this.query<Raven.Server.Dashboard.ThreadInfo[]>(url, null, null,  x => x["Runaway Threads"].List)
            .fail((response: JQueryXHR) => this.reportError("Failed to load Threads Runtime Info", response.responseText, response.statusText));
    }

}

export = getDebugThreadsRunawayCommand;
