import viewModelBase from "viewmodels/viewModelBase";
import validateTwoFactorSecretCommand from "commands/auth/validateTwoFactorSecretCommand";


class twoFactorShell extends viewModelBase {
    view = require("views/twoFactorShell.html");
    
    
    compositionComplete() {
        super.compositionComplete();
        
        const response = prompt("Enter 6 digits code");
        
        //TODO: extra params!
        
        new validateTwoFactorSecretCommand(response)
            .execute();
    }
}


export = twoFactorShell;
