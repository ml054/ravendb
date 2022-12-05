﻿import React from "react";
import viewModelBase from "viewmodels/viewModelBase";
import database from "models/resources/database";

abstract class reactViewModelBase extends viewModelBase {

    view = { default: `<div class="react-container" data-bind="react: reactOptions"></div>` };

    private readonly db?: database;
    private readonly reactView: React.FC<any>;
    private readonly bootstrap5: boolean; //TODO: will be removed once we migrate all react views to bs5 (I assume one left)

    protected constructor(reactView: React.FC<any>, bootstrap5 = true) {
        super();

        this.reactView = reactView;
        this.bootstrap5 = bootstrap5;
    }

    isUsingBootstrap5() {
        return this.bootstrap5;
    }

    reactOptions: ReactInKnockout<any>;

    activate(args: any, parameters?: any) {
        super.activate(args, parameters);

        this.reactOptions = this.createReactOptions(this.reactView, {
            ...args,
            db: this.activeDatabase()
        });
    }

    createReactOptions<TProps = unknown>(component: (props?: TProps) => JSX.Element, props?: TProps) {
        return ko.pureComputed(() => ({
            component,
            props
        }));
    }
}


export = reactViewModelBase;
