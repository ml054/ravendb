/// <reference path="../../../../typings/tsd.d.ts"/>
import database from "models/resources/database";
import getOngoingTaskInfoCommand from "commands/database/tasks/getOngoingTaskInfoCommand";
import app from "durandal/app";
import subscriptionQueryDefinitionPreview from "viewmodels/database/status/subscriptionQueryDefinitionPreview";

class subscriptionQueryDefinitionCache {
    private readonly db: database;

    constructor(db: database) {
        this.db = db;
    }

    showDefinitionFor(taskId: number, taskName: string) {
        const command = getOngoingTaskInfoCommand.forSubscription(this.db, taskId, taskName);

        const task = command.execute();

        const dialog = new subscriptionQueryDefinitionPreview(task);
        app.showBootstrapDialog(dialog);
    }
}

export = subscriptionQueryDefinitionCache;
