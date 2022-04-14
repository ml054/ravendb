/// <reference path="../../../../../typings/tsd.d.ts"/>
import ongoingTaskListModel from "models/database/tasks/ongoingTaskListModel";

abstract class serverWideTaskListModel extends ongoingTaskListModel {

    excludedDatabases = ko.observableArray<string>();
}

export = serverWideTaskListModel;
