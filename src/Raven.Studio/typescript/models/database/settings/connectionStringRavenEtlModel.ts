/// <reference path="../../../../typings/tsd.d.ts"/>
import database from "models/resources/database";
import connectionStringModel from "models/database/settings/connectionStringModel";
import saveConnectionStringCommand from "commands/database/settings/saveConnectionStringCommand";
import testClusterNodeConnectionCommand from "commands/database/cluster/testClusterNodeConnectionCommand";
import jsonUtil from "common/jsonUtil";
import discoveryUrl from "models/database/settings/discoveryUrl";

class connectionStringRavenEtlModel extends connectionStringModel { 

    static serverWidePrefix = "Server Wide Raven Connection String";
    isServerWide: KnockoutComputed<boolean>;
    
    database = ko.observable<string>();
    topologyDiscoveryUrls = ko.observableArray<discoveryUrl>([]);
    inputUrl = ko.observable<discoveryUrl>(new discoveryUrl(""));
    selectedUrlToTest = ko.observable<string>();

    validationGroup: KnockoutValidationGroup;
    
    dirtyFlag: () => DirtyFlag;
    
    constructor(dto: Raven.Client.Documents.Operations.ETL.RavenConnectionString, isNew: boolean, tasks: { taskName: string; taskId: number }[]) {
        super(isNew, tasks);
        
        this.update(dto);
        this.initValidation(); 
        
        const urlsCount = ko.pureComputed(() => this.topologyDiscoveryUrls().length);
        const urlsAreDirty = ko.pureComputed(() => {
            let anyDirty = false;
            
            this.topologyDiscoveryUrls().forEach(url => {
                if (url.dirtyFlag().isDirty()) {
                    anyDirty = true;
                }
            });
           
            return anyDirty;
        });
        
        this.dirtyFlag = new ko.DirtyFlag([
            this.database,
            this.connectionStringName,
            urlsCount,
            urlsAreDirty
        ], false, jsonUtil.newLineNormalizingHashFunction);
        
        this.isServerWide = ko.pureComputed(() => {
            return this.connectionStringName().startsWith(connectionStringRavenEtlModel.serverWidePrefix);
        });
    }

    update(dto: Raven.Client.Documents.Operations.ETL.RavenConnectionString) {
        super.update(dto);
        
        this.connectionStringName(dto.Name); 
        this.database(dto.Database);
        this.topologyDiscoveryUrls(dto.TopologyDiscoveryUrls.map((x) => new discoveryUrl(x)));
    }

    initValidation() {
        super.initValidation();
        
        this.database.extend({
            required: true,
            validDatabaseName: true
        });

        this.topologyDiscoveryUrls.extend({
            validation: [
                {
                    validator: () => this.topologyDiscoveryUrls().length > 0,
                    message: "At least one discovery url is required"
                }
            ]
        });
       
        this.validationGroup = ko.validatedObservable({
            connectionStringName: this.connectionStringName,
            database: this.database,
            topologyDiscoveryUrls: this.topologyDiscoveryUrls
        });
    }

    static empty(): connectionStringRavenEtlModel {
        return new connectionStringRavenEtlModel({
            Type: "Raven",
            Name: "", 
            TopologyDiscoveryUrls: [],
            Database: ""
        }, true, []);
    }
    
    toDto() {
        return {
            Type: "Raven",
            Name: this.connectionStringName(),
            TopologyDiscoveryUrls: this.topologyDiscoveryUrls().map((x) => x.discoveryUrlName()),
            Database: this.database()
        };
    }
    
    removeDiscoveryUrl(url: discoveryUrl) {
        this.topologyDiscoveryUrls.remove(url); 
    }

    addDiscoveryUrlWithBlink() { 
        if ( !_.find(this.topologyDiscoveryUrls(), x => x.discoveryUrlName() === this.inputUrl().discoveryUrlName())) {
            const newUrl = new discoveryUrl(this.inputUrl().discoveryUrlName());
            newUrl.dirtyFlag().forceDirty();
            this.topologyDiscoveryUrls.unshift(newUrl);
            this.inputUrl().discoveryUrlName("");

            $(".collection-list li").first().addClass("blink-style");
        }
    }

    testConnection(urlToTest: discoveryUrl) : JQueryPromise<Raven.Server.Web.System.NodeConnectionTestResult> {
        return new testClusterNodeConnectionCommand(urlToTest.discoveryUrlName(), this.database(), false)
            .execute()
            .done((result) => {
                if (result.Error) {
                    urlToTest.hasTestError(true);
                }
            });
    }

    saveConnectionString(db: database) : JQueryPromise<void> {
        return new saveConnectionStringCommand(db, this)
            .execute();
    }
}

export = connectionStringRavenEtlModel;
