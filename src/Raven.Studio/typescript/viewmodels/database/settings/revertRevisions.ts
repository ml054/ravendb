import viewModelBase from "viewmodels/viewModelBase";
import datePickerBindingHandler from "common/bindingHelpers/datePickerBindingHandler";
import revertRevisionsCommand from "commands/database/documents/revertRevisionsCommand";
import revertRevisionsRequest from "models/database/documents/revertRevisionsRequest";
import notificationCenter from "common/notifications/notificationCenter";
import appUrl from "common/appUrl";
import moment from "moment";

class revertRevisions extends viewModelBase {
    
    view = require("views/database/settings/revertRevisions.html");

    model = new revertRevisionsRequest();
    revisionsUrl: KnockoutComputed<string>;
    
    datePickerOptions = {
        format: revertRevisionsRequest.defaultDateFormat,
        maxDate: moment.utc().add(10, "minutes").toDate() // add 10 minutes to avoid issues with time skew
    };
    
    spinners = {
        revert: ko.observable<boolean>(false)
    };
    
    static magnitudes: timeMagnitude[] = ["minutes", "hours", "days"];

    constructor() {
        super();

        this.revisionsUrl = ko.pureComputed(() => {
            return appUrl.forRevisions(this.activeDatabase());
        });
        
        this.bindToCurrentInstance("setMagnitude");
        datePickerBindingHandler.install();
    }
    
    setMagnitude(value: timeMagnitude) {
        this.model.windowMagnitude(value);
    }
    
    run() {
        if (this.isValid(this.model.validationGroup)) {
            const db = this.activeDatabase();
            
            this.confirmationMessage("Revert Revisions", "Do you want to revert documents state to date: " + this.model.pointInTimeFormatted() + " UTC?", {
                buttons: ["No", "Yes, revert"]
                })
                .done(result => {
                    if (result.can) {
                        this.spinners.revert(true);
                        
                        const dto = this.model.toDto();
                        new revertRevisionsCommand(dto, db)
                            .execute()
                            .done((operationIdDto: operationIdDto) => {
                                const operationId = operationIdDto.OperationId;
                                notificationCenter.instance.openDetailsForOperationById(db, operationId);
                            })
                            .always(() => this.spinners.revert(false));
                    }
                })
        }
    }
}

export = revertRevisions;
