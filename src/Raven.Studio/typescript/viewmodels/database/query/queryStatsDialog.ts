import appUrl from "common/appUrl";
import dialog from "plugins/dialog";
import database from "models/resources/database";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";

class queryStatsDialog extends dialogViewModelBase {

    view = require("views/database/query/queryStatsDialog.html");

    public static readonly AllDocs = "AllDocs";

    selectedIndexEditUrl: string;

    canNavigateToIndex: boolean;
    
    isGraphQuery: boolean;
    
    totalResults: string;

    constructor(private queryStats: Raven.Client.Documents.Queries.QueryResult<any, any>, totalResults: string, private db: database) {
        super();

        this.selectedIndexEditUrl = appUrl.forEditIndex(queryStats.IndexName, this.db);

        this.canNavigateToIndex = this.physicalIndexExists(queryStats.IndexName);
        this.isGraphQuery = queryStats.IndexName === "@graph";
        this.totalResults = totalResults;
    }

    private physicalIndexExists(indexName: string) {
        if (!indexName || indexName === queryStatsDialog.AllDocs) {
            return false;
        }
        if (indexName.startsWith("collection/")) {
            return false;
        }
        return true;
    }
    
    cancel() {                
        dialog.close(this);
    }

}

export = queryStatsDialog;
