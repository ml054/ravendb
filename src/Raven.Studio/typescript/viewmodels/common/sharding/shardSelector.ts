import shard = require("models/resources/shard");
import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
import inlineShardSelector = require("viewmodels/common/sharding/inlineShardSelector");

class shardSelector extends dialogViewModelBase {
    
    readonly selector: inlineShardSelector;
    private readonly canClose: boolean;
    private readonly onClose: () => void;
    
    private readonly onShardSelected: (shard: shard, nodeTag: string) => void;
    
    view = require("views/common/sharding/shardSelector.html");
    
    constructor(onShardSelected: (shard: shard, nodeTag: string) => void, onClose?: () => void) {
        super();
        this.canClose = !!onClose;
        this.onClose = onClose;
        this.selector = new inlineShardSelector(this.activeDatabase());
        
        this.onShardSelected = onShardSelected;
        
        this.bindToCurrentInstance( "shardSelected");
    }

    closeClicked() {
        this.onClose();
    }
    
    shardSelected() {
        this.onShardSelected(this.selector.form.shard(), this.selector.form.node());
    }
}

export = shardSelector;
