/// <reference path="../typings/tsd.d.ts" />

import { overrideViews } from "./overrides/views";
import { overrideComposition } from "./overrides/composition";
import { overrideSystem } from "./overrides/system";

import "durandal/../css/durandal.css";

import system from "durandal/system";
import app from "durandal/app";

overrideSystem();
overrideComposition();
overrideViews();

const ko = require("knockout");
require("knockout.validation");
require("knockout-postbox");
require("knockout-delegated-events"); 
const { DirtyFlag } = require("./external/dirtyFlag");
ko.DirtyFlag = DirtyFlag;

system.debug(true);

app.title = "Raven.Studio";

const router = require('plugins/router');
router.install();

const bootstrapModal = require("durandalPlugins/bootstrapModal");
bootstrapModal.install();

const dialog = require("plugins/dialog");
dialog.install({});

const pluginWidget = require("plugins/widget");
pluginWidget.install({});

app.start().then(() => {
    if ("WebSocket" in window) {
        if (window.location.pathname.startsWith("/studio")) {
            const shell = require("./viewmodels/shell");
            app.setRoot(shell);
        } else if (window.location.pathname.startsWith("/eula")) {
            app.setRoot("viewmodels/eulaShell");
        } else {
            app.setRoot("viewmodels/wizard/setupShell")
        }
    } else {
        //The browser doesn't support WebSocket
        app.showBootstrapMessage("Your browser isn't supported. Please use a modern browser!", ":-(", []);
    }
});
