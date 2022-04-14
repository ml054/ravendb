/// <reference path="../../typings/tsd.d.ts" />

import router from "plugins/router";
import sys from "durandal/system";
import eulaRoutes from "common/eula/routes";
import getClientBuildVersionCommand from "commands/database/studio/getClientBuildVersionCommand";
import getServerBuildVersionCommand from "commands/resources/getServerBuildVersionCommand";
import messagePublisher from "common/messagePublisher";
import extensions from "common/extensions";
import viewModelBase from "viewmodels/viewModelBase";
import autoCompleteBindingHandler from "common/bindingHelpers/autoCompleteBindingHandler";
import requestExecution from "common/notifications/requestExecution";
import protractedCommandsDetector from "common/notifications/protractedCommandsDetector";
import buildInfo from "models/resources/buildInfo";
import constants from "common/constants/constants";

class eulaShell extends viewModelBase {

    view = require("views/eulaShell.html");

    private router = router;
    studioLoadingFakeRequest: requestExecution;
    clientBuildVersion = ko.observable<clientBuildVersionDto>();
    static buildInfo = buildInfo;

    showSplash = viewModelBase.showSplash;

    constructor() {
        super();

        autoCompleteBindingHandler.install();

        this.studioLoadingFakeRequest = protractedCommandsDetector.instance.requestStarted(0);
        
        extensions.install();
    }

    // Override canActivate: we can always load this page, regardless of any system db prompt.
    canActivate(args: any): any {
        return true;
    }

    activate(args: any) {
        super.activate(args, { shell: true });

        this.setupRouting();
        
        return this.router.activate()
            .then(() => {
                this.fetchClientBuildVersion();
                this.fetchServerBuildVersion();
            })
    }

    private fetchServerBuildVersion() {
        new getServerBuildVersionCommand()
            .execute()
            .done((serverBuildResult: serverBuildVersionDto) => {
                buildInfo.serverBuildVersion(serverBuildResult);

                const currentBuildVersion = serverBuildResult.BuildVersion;
                if (currentBuildVersion !== constants.DEV_BUILD_NUMBER) {
                    buildInfo.serverMainVersion(Math.floor(currentBuildVersion / 10000));
                }
            });
    }

    private fetchClientBuildVersion() {
        new getClientBuildVersionCommand()
            .execute()
            .done((result: clientBuildVersionDto) => {
                this.clientBuildVersion(result);
                viewModelBase.clientVersion(result.Version);
            });
    }

    private setupRouting() {
        router.map(eulaRoutes.get()).buildNavigationModel();

        router.mapUnknownRoutes((instruction: DurandalRouteInstruction) => {
            const queryString = !!instruction.queryString ? ("?" + instruction.queryString) : "";

            messagePublisher.reportError("Unknown route", "The route " + instruction.fragment + queryString + " doesn't exist, redirecting...");

            location.href = "#license";
        });
    }

    attached() {
        super.attached();

        sys.error = (e: any) => {
            console.error(e);
            messagePublisher.reportError("Failed to load routed module!", e);
        };
    }

    compositionComplete() {
        super.compositionComplete();
        $("body")
            .removeClass('loading-active')
            .addClass("setup-shell");
        $(".loading-overlay").remove();

        this.studioLoadingFakeRequest.markCompleted();
        this.studioLoadingFakeRequest = null;
    }
}

export = eulaShell;
