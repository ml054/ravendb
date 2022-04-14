import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";
import document from "models/database/documents/document";

class getRevisionsBinEntryCommand extends commandBase {

    constructor(private database: database, private changeVector: string, private take: number) {
        super();
    }

    execute(): JQueryPromise<pagedResult<document>> {
        const args = {
            changeVector: this.changeVector,
            pageSize: this.take
        };

        const resultsSelector = (dto: resultsDto<documentDto>, xhr: JQueryXHR): pagedResult<document> => {
            return {
                items: dto.Results.map(x => new document(x)),
                totalResultCount: -1,
                resultEtag: this.extractEtag(xhr)
            };
        };
        const url = endpoints.databases.revisions.revisionsBin + this.urlEncodeArgs(args);
        return this.query(url, null, this.database, resultsSelector)
            .fail((response: JQueryXHR) => this.reportError("Failed to get revision bin entries", response.responseText, response.statusText));
    }

}

export = getRevisionsBinEntryCommand;
