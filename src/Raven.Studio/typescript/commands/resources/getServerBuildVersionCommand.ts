import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getServerBuildVersionCommand extends commandBase {

    execute() {
        const args = {
            t: new Date().getTime()
        };
        return this.query<serverBuildVersionDto>(endpoints.global.buildVersion.buildVersion, args);
    }
}

export = getServerBuildVersionCommand;
