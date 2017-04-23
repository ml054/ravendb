/// <reference path="../../typings/tsd.d.ts"/>

import pluralizeHelpers = require("common/helpers/text/pluralizeHelpers");
import dialog = require("plugins/dialog");

type dialogViewModelBaseOptions = {
    elementToFocusOnDismissal?: string;
    dialogSelectorName?: string;
}

abstract class dialogViewModelBase {
    static readonly dialogSelector = ".modal-dialog";

    private onEnterBinding: JwertySubscription;
    private readonly elementToFocusOnDismissal: string;
    private readonly dialogSelector: string;
    private disposableActions: Array<disposable> = [];

    pluralize = pluralizeHelpers.pluralize;

    constructor(options?: dialogViewModelBaseOptions) {
        if (options) {
            this.elementToFocusOnDismissal = options.elementToFocusOnDismissal;
            this.dialogSelector = options.dialogSelectorName || dialogViewModelBase.dialogSelector;
        } else {
            this.dialogSelector = dialogViewModelBase.dialogSelector;
        }
    }

    protected bindToCurrentInstance(...methods: Array<keyof this>) {
        _.bindAll(this, ...methods);
    }

    attached() {
        this.onEnterBinding = jwerty.key("enter", () => this.enterKeyPressed());
    }

    deactivate(args: any) {
        if (this.onEnterBinding) {
            this.onEnterBinding.unbind();
        }

        this.disposableActions.forEach(f => f.dispose());
        this.disposableActions = [];
    }

    detached() {
        if (this.elementToFocusOnDismissal) {
            $(this.elementToFocusOnDismissal).focus();
        }
    }

    compositionComplete(view?: any, parent?: any) {
        setTimeout(() => this.setInitialFocus(), 100); // We have to time-delay this, else it never receives focus.
    }

    close() {
        dialog.close(this);
    }

    protected registerDisposable(disposable: disposable) {
        this.disposableActions.push(disposable);
    }

    protected setInitialFocus() {
        const autoFocusElement = $(this.dialogSelector + " [autofocus]");
        if (autoFocusElement.length) {
            autoFocusElement.focus();
            autoFocusElement.select();
        } else {
            $(this.dialogSelector).focus();
        }
    }

    protected enterKeyPressed(): boolean {
        const confirmButton = this.getCofirmButton();
        if (confirmButton && confirmButton.click) {
            confirmButton.click();
        }

        return true;
    }

    protected getCofirmButton(): HTMLElement {
        return $(".modal-footer:visible .btn-primary")[0] as HTMLElement;
    }

    protected isValid(context: KnockoutValidationGroup, showErrors = true): boolean {
        if (context.isValid()) {
            return true;
        } else {
            if (showErrors) {
                context.errors.showAllMessages();
            }
            return false;
        }
    }

}

export = dialogViewModelBase;
