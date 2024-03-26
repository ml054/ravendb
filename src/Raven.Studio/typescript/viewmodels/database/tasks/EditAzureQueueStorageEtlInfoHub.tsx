import AboutViewFloating, { AccordionItemWrapper } from "components/common/AboutView";
import { licenseSelectors } from "components/common/shell/licenseSlice";
import { useAppSelector } from "components/store";
import React from "react";
import { Icon } from "components/common/Icon";
import { useRavenLink } from "hooks/useRavenLink";
import FeatureAvailabilitySummaryWrapper, { FeatureAvailabilityData } from "components/common/FeatureAvailabilitySummary";
import { useLimitedFeatureAvailability } from "components/utils/licenseLimitsUtils";

export function EditAzureQueueStorageEtlInfoHub() {
    const hasQueueEtl = useAppSelector(licenseSelectors.statusValue("HasQueueEtl"));
    const featureAvailability = useLimitedFeatureAvailability({
        defaultFeatureAvailability,
        overwrites: [
            {
                featureName: defaultFeatureAvailability[0].featureName,
                value: hasQueueEtl,
            },
        ],
    });
    const azureQueueStorageEtlDocsLink = useRavenLink({ hash: "KFKQM7" });
    return (
        <AboutViewFloating defaultOpen={hasQueueEtl ? null : "licensing"}>
            <AccordionItemWrapper
                targetId="about"
                icon="about"
                color="info"
                heading="About this view"
                description="Get additional info on this feature"
            >
            </AccordionItemWrapper>
            <FeatureAvailabilitySummaryWrapper
                isUnlimited={hasQueueEtl}
                data={featureAvailability}
            />
        </AboutViewFloating>
    );
}

const defaultFeatureAvailability: FeatureAvailabilityData[] = [
    {
        featureName: "Azure Queue Storage ETL",
        featureIcon: "azure-queue-storage-etl",
        community: { value: false },
        professional: { value: false },
        enterprise: { value: true },
    },
];
