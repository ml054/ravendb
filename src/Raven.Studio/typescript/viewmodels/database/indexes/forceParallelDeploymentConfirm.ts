import dialog from "plugins/dialog";
import database from "models/resources/database";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import indexProgress from "models/database/index/indexProgressOLD";
import finishRollingCommand from "commands/database/index/finishRollingCommand";

class forceParallelDeploymentConfirm extends dialogViewModelBase {
    view = require("views/database/indexes/forceParallelDeploymentConfirm.html");
    /*

    TODO:
    
    localNodeTag: string;
    indexName: string;
    canForceCurrentNode: boolean;
    
    constructor(progress: indexProgress, localNodeTag: string, private db: database) {
        super();
        
        this.indexName = progress.name;
        this.localNodeTag = localNodeTag;
        this.canForceCurrentNode = progress.rollingProgress().find(x => x.nodeTag === localNodeTag).state() !== "Done";
    }

    forceDeploymentMode(currentNodeOnly: boolean) {
        new finishRollingCommand(this.db, this.indexName, currentNodeOnly ? this.localNodeTag : null)
            .execute();

        dialog.close(this);
    }

    cancel() {
        dialog.close(this);
    }*/
}

export = forceParallelDeploymentConfirm;
