import dialog from "plugins/dialog";
import collection from "models/database/documents/collection";
import dialogViewModelBase from "viewmodels/dialogViewModelBase";
import collectionsTracker from "common/helpers/database/collectionsTracker";

class deleteCollection extends dialogViewModelBase {
    
    view = require("views/database/documents/deleteCollection.html");
    
    private deletionStarted = false;    
    isAllDocuments: boolean;
    hasHiloDocuments: boolean;

    constructor(private collectionName: string, private itemsToDelete: number ) {
        super();
        this.isAllDocuments = collection.allDocumentsCollectionName === collectionName;
        this.hasHiloDocuments = collectionsTracker.default.getCollectionCount(collection.hiloCollectionName) > 0;
    }

    deleteCollection() {
        this.deletionStarted = true;
        dialog.close(this, true);
    }

    cancel() {
        dialog.close(this, false);
    }

    deactivate() {
        super.deactivate(null);
    }
}

export = deleteCollection;
