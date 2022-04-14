import appUrl from "common/appUrl";
import dialog from "plugins/dialog";
import database from "models/resources/database";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import explainQueryCommand from "commands/database/index/explainQueryCommand";

class explainQueryDialog extends dialogViewModelBase {

    view = require("views/database/query/explainQueryDialog.html");
    
    explanation = ko.observableArray<Raven.Server.Documents.Queries.Dynamic.DynamicQueryToIndexMatcher.Explanation>([]);
    indexUsed = ko.observable<string>();
    
    constructor(private response: explainQueryResponse) {
        super();
        
        this.explanation(response.Results);
        this.indexUsed(response.IndexName);
    }
}

export = explainQueryDialog;
