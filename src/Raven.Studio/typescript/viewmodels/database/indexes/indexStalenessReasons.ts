import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import database from "models/resources/database";
import getIndexStalenessReasonsCommand from "commands/database/index/getIndexStalenessReasonsCommand";

class indexStalenessReasons extends dialogViewModelBase {

    view = require("views/database/indexes/indexStalenessReasons.html");
    
    private db: database;
    indexName: string;
    reasons = ko.observable<indexStalenessReasonsResponse>();
    location: databaseLocationSpecifier;
    
    constructor(db: database, indexName: string, location?: databaseLocationSpecifier) {
        super();
        this.db = db;
        this.indexName = indexName;
        this.location = location;
    }
    
    activate() {
        return new getIndexStalenessReasonsCommand(this.indexName, this.db, this.location)
            .execute()
            .done(reasons => {
                this.reasons(reasons);
            });
    }

}

export = indexStalenessReasons;
