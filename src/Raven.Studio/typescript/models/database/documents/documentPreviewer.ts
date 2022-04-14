import app from "durandal/app";
import document from "models/database/documents/document";
import documentMetadata from "models/database/documents/documentMetadata";
import showDataDialog from "viewmodels/common/showDataDialog";
import database from "models/resources/database";
import viewHelpers from "common/helpers/view/viewHelpers";
import getDocumentWithMetadataCommand from "commands/database/documents/getDocumentWithMetadataCommand";

class documentPreviewer {
    static preview(documentId: KnockoutObservable<string>, db: KnockoutObservable<database>, validationGroup: KnockoutValidationGroup, spinner?: KnockoutObservable<boolean>){
        if (spinner) {
            spinner(true);
        }
        viewHelpers.asyncValidationCompleted(validationGroup)
            .then(() => {
                if (viewHelpers.isValid(validationGroup)) {
                    new getDocumentWithMetadataCommand(documentId(), db())
                        .execute()
                        .done((doc: document) => {
                            const docDto = doc.toDto(true);
                            const metaDto = docDto["@metadata"];
                            documentMetadata.filterMetadata(metaDto);
                            const text = JSON.stringify(docDto, null, 4);
                            app.showBootstrapDialog(new showDataDialog("Document: " + doc.getId(), text, "javascript"));
                        })
                        .always(() => spinner(false));
                } else {
                    if (spinner) {
                        spinner(false);
                    }
                }
            });
    }
}

export = documentPreviewer;
