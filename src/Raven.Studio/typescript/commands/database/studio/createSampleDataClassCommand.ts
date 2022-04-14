import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class createSampleDataClassCommand extends commandBase {
    constructor(private db: database) {
        super();
    }

    execute(): JQueryPromise<string> {
        return this.query<string>(endpoints.databases.sampleData.studioSampleDataClasses, null, this.db, null, { dataType: 'text' });
     }
}

export = createSampleDataClassCommand;
