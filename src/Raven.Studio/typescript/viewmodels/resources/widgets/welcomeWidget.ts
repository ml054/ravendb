import widget from "viewmodels/resources/widgets/widget";
import app from "durandal/app";
import appUrl from "common/appUrl";
import createDatabase from "viewmodels/resources/createDatabase";
import viewModelBase from "viewmodels/viewModelBase";

class welcomeWidget extends widget {

    static clientVersion = viewModelBase.clientVersion;
    
    view = require("views/resources/widgets/welcomeWidget.html");
    
    clusterViewUrl = appUrl.forCluster();
    connectingToDatabaseUrl = welcomeWidget.createLink("GXMEFO");
    indexesUrl = welcomeWidget.createLink("7D62W8");
    queryingUrl = welcomeWidget.createLink("L1QXE3");
    
    private static createLink(hash: string) {
        return ko.pureComputed(() => {
            const version = welcomeWidget.clientVersion();
            return `https://ravendb.net/l/${hash}/${version}`;
        });
    }
    
    getType(): widgetType {
        return "Welcome";
    }

    newDatabase() {
        const createDbView = new createDatabase("newDatabase");
        app.showBootstrapDialog(createDbView);
    }
}

export = welcomeWidget;
