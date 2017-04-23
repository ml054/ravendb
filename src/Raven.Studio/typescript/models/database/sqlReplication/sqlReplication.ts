import sqlReplicationTable = require("models/database/sqlReplication/sqlReplicationTable");
import document = require("models/database/documents/document");
import documentMetadata = require("models/database/documents/documentMetadata");
import aceEditorBindingHandler = require("common/bindingHelpers/aceEditorBindingHandler");

class sqlReplication extends document {

    /*CONNECTION_STRING = "Connection String";
    PREDEFINED_CONNECTION_STRING_NAME = "Predefined Connection String Name";
    CONNECTION_STRING_NAME = "Connection String Name";
    CONNECTION_STRING_SETTING_NAME = "Connection String Setting Name";
    
    availableConnectionStringTypes = [
        this.PREDEFINED_CONNECTION_STRING_NAME,
        this.CONNECTION_STRING ,
        this.CONNECTION_STRING_NAME,
        this.CONNECTION_STRING_SETTING_NAME 
    ];

    metadata: documentMetadata;

    name = ko.observable<string>().extend({ required: true });
    disabled = ko.observable<boolean>().extend({ required: true });
    connectionStringType = ko.observable<string>().extend({ required: true });
    connectionStringValue = ko.observable<string>(null).extend({ required: true });
    collection = ko.observable<string>("");
    parameterizeDeletesDisabled = ko.observable<boolean>(false).extend({ required: true });
    forceSqlServerQueryRecompile = ko.observable<boolean>(false);
    quoteTables = ko.observable<boolean>(true);
    sqlReplicationTables = ko.observableArray<sqlReplicationTable>().extend({ required: true });
    script = ko.observable<string>("").extend({ required: true });
    connectionString = ko.observable<string>(null);
    connectionStringName = ko.observable<string>(null);
    connectionStringSettingName = ko.observable<string>(null);
    connectionStringSourceFieldName: KnockoutComputed<string>;
    
    collections = ko.observableArray<string>();
    searchResults: KnockoutComputed<string[]>;
    isVisible = ko.observable<boolean>(true);
    
    showReplicationConfiguration = ko.observable<boolean>(false);

    hasAnyInsertOnlyOption = ko.computed(() => {
        var hasAny = false;
        this.sqlReplicationTables().forEach(s => {
            // don't use return here to register all deps in knockout
            if (s.insertOnly()) {
                hasAny = true;
            }
        });
        return hasAny;
    });

    constructor(dto: Raven.Server.Documents.ETL.Providers.SQL.SqlEtlConfiguration) {
        super(dto);

        //TODO: bind command timeout + lo

        this.name(dto.Name);
        this.disabled(dto.Disabled);
        this.collection(dto.Collection);
        this.parameterizeDeletesDisabled(dto.ParameterizeDeletesDisabled);
        this.sqlReplicationTables(dto.SqlTables.map(tab => new sqlReplicationTable(tab)));
        this.script(dto.Script);
        this.forceSqlServerQueryRecompile(!!dto.ForceSqlServerQueryRecompile? dto.ForceSqlServerQueryRecompile:false);
        this.quoteTables(("PerformTableQuatation" in dto) ? dto.QuoteTables : ("QuoteTables" in dto ? dto.QuoteTables : true));
        //TODO: this.setupConnectionString(dto);
        this.connectionStringType(this.PREDEFINED_CONNECTION_STRING_NAME); //TODO: delete 
        this.connectionStringValue(dto.ConnectionStringName); //TODO: delete

        this.metadata = new documentMetadata((dto as any)["@metadata"]);

        this.connectionStringSourceFieldName = ko.computed(() => {
            if (this.connectionStringType() == this.CONNECTION_STRING) {
                return "Connection String Text";
            } else if (this.connectionStringType() == this.PREDEFINED_CONNECTION_STRING_NAME) {
                return "Predefined connection string name";
            } else if (this.connectionStringType() == this.CONNECTION_STRING_NAME) {
                return "Setting name in local machine configuration";
            } else {
                return "Setting name in memory/remote configuration";
            }
        });

        this.searchResults = ko.computed(() => {
            var collection: string = this.collection();
            return this.collections().filter((name) => name.toLowerCase().indexOf(collection.toLowerCase()) > -1);
        });
        
        this.script.subscribe((newValue) => {
            var message = "";
            var currentEditor = aceEditorBindingHandler.currentEditor;
            var textarea: any = $(currentEditor.container).find("textarea")[0];

            if (newValue === "") {
                message = "Please fill out this field.";
            }
            textarea.setCustomValidity(message);
            setTimeout(() => {
                var annotations = currentEditor.getSession().getAnnotations();
                var isErrorExists = false;
                for (var i = 0; i < annotations.length; i++) {
                    var annotationType = annotations[i].type;
                    if (annotationType === "error" || annotationType === "warning") {
                        isErrorExists = true;
                        break;
                    }
                }
                if (isErrorExists) {
                    message = "The script isn't a javascript legal expression!";
                    textarea.setCustomValidity(message);
                }
            }, 700);
        });
    }

    /*TODO private setupConnectionString(dto: sqlReplicationDto) {
        
        if (dto.ConnectionStringName) {
            this.connectionStringType(this.CONNECTION_STRING_NAME);
            this.connectionStringValue(dto.ConnectionStringName);
        } else if (dto.ConnectionStringSettingName) {
            this.connectionStringType(this.CONNECTION_STRING_SETTING_NAME);
            this.connectionStringValue(dto.ConnectionStringSettingName);
        } else if (dto.ConnectionString){
            this.connectionStringType(this.CONNECTION_STRING);
            this.connectionStringValue(dto.ConnectionString);
        }
        else {
            this.connectionStringType(this.PREDEFINED_CONNECTION_STRING_NAME);
            this.connectionStringValue(dto.PredefinedConnectionStringSettingName);
        }
    }*

    setConnectionStringType(strType: string) {
        this.connectionStringType(strType);
    }

    static empty(): sqlReplication {
        return new sqlReplication({
            Name: "",
            Disabled: true,
            ParameterizeDeletesDisabled: false,
            Collection: "",
            Script: "",
            Id: null,
            FactoryName: null,
            ConnectionString: null,
            PredefinedConnectionStringSettingName:null,
            ConnectionStringName: null,
            ConnectionStringSettingName: null,
            SqlTables: [sqlReplicationTable.empty().toDto()],
            ForceSqlServerQueryRecompile: false,
            QuoteTables: true,
            CommandTimeout: null,
            HasLoadAttachment: false
        } as Raven.Server.Documents.ETL.Providers.SQL.SqlEtlConfiguration);
    }

    toDto(): Raven.Server.Documents.ETL.Providers.SQL.SqlEtlConfiguration {
        //TODO:var meta = this.__metadata.toDto();
        //TODO: meta["@id"] = "Raven/SqlReplication/Configuration/" + this.name();
        return {
            //TODO: '@metadata': meta,
            //TODO: Id: null as any,
            Name: this.name(),
            Disabled: this.disabled(),
            ParameterizeDeletesDisabled: this.parameterizeDeletesDisabled(),
            Collection: this.collection(),
            Script: this.script(),
            //TODO: FactoryName: this.factoryName(),
            //TODO: ConnectionString: this.prepareConnectionString(this.CONNECTION_STRING),
            //TODO: PredefinedConnectionStringSettingName: this.prepareConnectionString(this.PREDEFINED_CONNECTION_STRING_NAME),
            ConnectionStringName: this.prepareConnectionString(this.PREDEFINED_CONNECTION_STRING_NAME),//TODO: revert CONNECTION_STRING_NAME
            //TODO: ConnectionStringSettingName: this.prepareConnectionString(this.CONNECTION_STRING_SETTING_NAME),
            ForceSqlServerQueryRecompile: this.forceSqlServerQueryRecompile(),
            QuoteTables: this.quoteTables(),
            SqlTables: this.sqlReplicationTables().map(tab => tab.toDto()),
            CommandTimeout: null,
            HasLoadAttachment: false //TODO: assign me!
        };
    }

    private prepareConnectionString(expectedType: string): string {
        return ((this.connectionStringType() === expectedType) ? this.connectionStringValue() : null);
    }

    enable() {
        this.disabled(false);
    }

    disable() {
        this.disabled(true);
    }

    enableParameterizeDeletes() {
        this.parameterizeDeletesDisabled(false);
    }

    disableParameterizeDeletes() {
        this.parameterizeDeletesDisabled(true);
    }

    addNewTable() {
        this.sqlReplicationTables.push(sqlReplicationTable.empty());
    }

    removeTable(table: sqlReplicationTable) {
        this.sqlReplicationTables.remove(table);
    }

    /* TODO
    setIdFromName() {
        this.__metadata.id = "Raven/SqlReplication/Configuration/" + this.name();
    }*

    saveNewCollectionName(newCollection: string) {
        this.collection(newCollection);
    }


    isSqlServerKindOfFactory(factoryName:string): boolean {
        if (factoryName == "System.Data.SqlClient" || factoryName == "System.Data.SqlServerCe.4.0" || factoryName == "System.Data.SqlServerCe.3.5") {
            return true;
        }
        return false;
    }*/
}

export = sqlReplication;
