import commandBase = require("commands/commandBase");
import sqlReplication = require("models/database/sqlReplication/sqlReplication");
import database = require("models/resources/database");

class getSqlReplicationsCommand extends commandBase {

    /*constructor(private db: database, private sqlReplicationName:string = null) {
        super();
    }

    execute(): JQueryPromise<Array<sqlReplication>> {
        var args = {
            startsWith: "Raven/SqlReplication/Configuration/",
            exclude: <string>null,
            start: 0,
            pageSize: 256
        };

        return this.query("/docs", args, this.db,
            (dtos: queryResultDto<Raven.Server.Documents.ETL.Providers.SQL.SqlEtlConfiguration>) => dtos.Results.map(dto => new sqlReplication(dto)));//TODO: use endpoints
    }*/
}

export = getSqlReplicationsCommand;
