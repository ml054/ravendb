import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import transformer = require("models/database/index/transformer");
import endpoints = require("endpoints");

class saveTransformerLockModeCommand extends commandBase {

    constructor(private transformers: Array<transformer>, private lockMode: Raven.Client.Documents.Transformers.TransformerLockMode, private db: database) {
        super();
    }

    execute(): JQueryPromise<void> {
        const args = {
            mode: this.lockMode,
            name: this.transformers.map(x => x.name())
        };
        
        const url = endpoints.databases.transformer.transformersSetLock + this.urlEncodeArgs(args);
        return this.post(url, JSON.stringify(args), this.db, { dataType: 'text' })
            .done(() => {
                const transformersNameStr: string = this.transformers.length === 1 ? this.transformers[0].name() : "Transformers";
                this.reportSuccess(`${transformersNameStr} mode was set to ${this.lockMode}`);
            })
            .fail((response: JQueryXHR) => this.reportError("Failed to set transformer lock mode", response.responseText));
    }
}

export = saveTransformerLockModeCommand; 
