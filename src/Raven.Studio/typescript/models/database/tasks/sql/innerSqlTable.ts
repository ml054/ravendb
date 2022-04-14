/// <reference path="../../../../../typings/tsd.d.ts"/>

import abstractSqlTable from "models/database/tasks/sql/abstractSqlTable";
import sqlReference from "models/database/tasks/sql/sqlReference";

class innerSqlTable extends abstractSqlTable {
    parentReference: sqlReference;
    
    constructor(parentReference: sqlReference) {
        super();
        this.parentReference = parentReference;
    }
    
    removeBackReference(reference: sqlReference) {
        const refToDelete = this.references().find(t => _.isEqual(t.joinColumns, reference.joinColumns)
            && t.targetTable.tableName === reference.sourceTable.tableName
            && t.targetTable.tableSchema === reference.targetTable.tableSchema);

        this.references.remove(refToDelete);
    }
}


export = innerSqlTable;
 
