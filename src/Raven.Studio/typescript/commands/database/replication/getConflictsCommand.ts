import commandBase from "commands/commandBase";
import database from "models/resources/database";
import endpoints from "endpoints";

class getConflictsCommand extends commandBase {

    constructor(private ownerDb: database, private start: number, private pageSize: number) {
        super();
    }

    execute(): JQueryPromise<pagedResult<replicationConflictListItemDto>> {
        const url = endpoints.databases.replication.replicationConflicts;

        const transformer = (result: resultsWithTotalCountDto<replicationConflictListItemDto>): pagedResult<replicationConflictListItemDto> => {
            return {
                items: result.Results,
                totalResultCount: result.TotalResults
            };
        }

        return this.query<pagedResult<replicationConflictListItemDto>>(url, null, this.ownerDb, transformer);
    }
}

export = getConflictsCommand;
