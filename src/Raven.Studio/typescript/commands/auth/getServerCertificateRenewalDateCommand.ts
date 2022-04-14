import commandBase from "commands/commandBase";
import endpoints from "endpoints";

class getServerCertificateRenewalDateCommand extends commandBase { 
    
    execute(): JQueryPromise<string> {
       
        const url = endpoints.global.adminCertificates.adminCertificatesLetsencryptRenewalDate;
        
        return this.query(url, null, null, x => x.EstimatedRenewal)
            .fail((response: JQueryXHR) => this.reportError("Failed to get the server certificate renewal date", response.responseText, response.statusText));
    }
}

export = getServerCertificateRenewalDateCommand;
