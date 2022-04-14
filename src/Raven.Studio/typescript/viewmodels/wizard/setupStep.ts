/// <reference path="../../../typings/tsd.d.ts" />

import viewModelBase from "viewmodels/viewModelBase";
import serverSetup from "models/wizard/serverSetup";

abstract class setupStep extends viewModelBase {
   protected model = serverSetup.default;
   
}

export = setupStep;
