import widget from "viewmodels/resources/widgets/widget";
import license from "models/auth/licenseModel";
import appUrl from "common/appUrl";
import generalUtils from "common/generalUtils";
import getCertificatesCommand from "commands/auth/getCertificatesCommand";
import accessManager from "common/shell/accessManager";
import clusterDashboard from "viewmodels/resources/clusterDashboard";
import moment from "moment";

interface serverCertificateInfo {
    dateFormatted: string;
    durationFormatted: string;
    expirationClass: string;
}

class licenseWidget extends widget {

    view = require("views/resources/widgets/licenseWidget.html");

    refreshIntervalId: number = -1;
    usingHttps = location.protocol === "https:";
    
    spinners = {
        serverCertificate: ko.observable<boolean>()
    }
    
    licenseTypeText = license.licenseTypeText;
    formattedExpiration = license.formattedExpiration;
    
    serverCertificateInfo = ko.observable<serverCertificateInfo>();

    aboutPageUrl = appUrl.forAbout();
    
    constructor(controller: clusterDashboard) {
        super(controller);
        
        this.spinners.serverCertificate(this.canLoadCertificateInfo());
    }

    private canLoadCertificateInfo() {
        return this.usingHttps && accessManager.default.isOperatorOrAbove();
    }

    compositionComplete() {
        super.compositionComplete();
        
        if (this.canLoadCertificateInfo()) {
            this.loadServerCertificate();
            this.refreshIntervalId = setInterval(() => this.loadServerCertificate(), 3600 * 1000);
        }
    }
    
    private loadServerCertificate() {
        new getCertificatesCommand(false)
            .execute()
            .done(certificatesInfo => {
                const serverCertificateThumbprint = certificatesInfo.LoadedServerCert;
                const serverCertificate = certificatesInfo.Certificates.find(x => x.Thumbprint === serverCertificateThumbprint);

                const date = moment.utc(serverCertificate.NotAfter);
                const dateFormatted = date.format("YYYY-MM-DD");

                const nowPlusMonth = moment.utc().add(1, 'months');
                
                let expirationClass: string = "";

                if (date.isBefore()) {
                    expirationClass = "text-danger";
                } else if (date.isAfter(nowPlusMonth)) {
                    // valid for at least 1 month - use defaults
                } else {
                    expirationClass = "text-warning";
                }
                
                const durationFormatted = generalUtils.formatDurationByDate(date, true);

                this.serverCertificateInfo({
                    dateFormatted,
                    expirationClass,
                    durationFormatted
                });
            })
            .always(() => this.spinners.serverCertificate(false));
    }

    isCloud = ko.pureComputed(() => {
        const licenseStatus = license.licenseStatus();
        return licenseStatus && licenseStatus.IsCloud;
    });

    expiresText = ko.pureComputed(() => {
        const licenseStatus = license.licenseStatus();
        if (!licenseStatus || !licenseStatus.Expiration) {
            return null;
        }

        return licenseStatus.IsIsv ? "Updates Expiration" : "License Expiration";
    });

    supportLabel = license.supportLabel;

    automaticRenewText = ko.pureComputed(() => {
        return this.isCloud() ? "Cloud licenses are automatically renewed" : "";
    });

    getType(): widgetType {
        return "License";
    }
    
    dispose() {
        super.dispose();
        
        if (this.refreshIntervalId !== -1) {
            clearInterval(this.refreshIntervalId);
        }
    }

}

export = licenseWidget;
