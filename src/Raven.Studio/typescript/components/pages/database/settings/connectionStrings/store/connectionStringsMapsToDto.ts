import { mapDestinationsToDto } from "components/common/formDestinations/utils/formDestinationsMapsToDto";
import {
    Connection,
    ConnectionStringDto,
    RavenConnection,
    SqlConnection,
    ElasticSearchConnection,
    KafkaConnection,
    RabbitMqConnection,
    //TODO: azure
    OlapConnection,
    ConnectionFormData,
} from "../connectionStringsTypes";
import assertUnreachable from "components/utils/assertUnreachable";
import ApiKeyAuthentication = Raven.Client.Documents.Operations.ETL.ElasticSearch.ApiKeyAuthentication;

export function mapRavenConnectionStringToDto(connection: RavenConnection): ConnectionStringDto {
    return {
        Type: "Raven",
        Name: connection.name,
        Database: connection.database,
        TopologyDiscoveryUrls: connection.topologyDiscoveryUrls.map((x) => x.url),
    };
}

export function mapSqlConnectionStringToDto(connection: SqlConnection): ConnectionStringDto {
    return {
        Type: "Sql",
        Name: connection.name,
        FactoryName: connection.factoryName,
        ConnectionString: connection.connectionString,
    };
}

export function mapOlapConnectionStringToDto(connection: OlapConnection): ConnectionStringDto {
    return {
        Type: "Olap",
        Name: connection.name,
        ...mapDestinationsToDto(connection.destinations),
    };
}

export function mapElasticSearchAuthenticationToDto(
    formValues: ConnectionFormData<ElasticSearchConnection>
): Raven.Client.Documents.Operations.ETL.ElasticSearch.Authentication {
    const auth = formValues.authMethodUsed;

    const apiKey: ApiKeyAuthentication =
        auth === "API Key" || auth === "Encoded API Key"
            ? {
                  ApiKey: formValues.authMethodUsed === "API Key" ? formValues.apiKey : undefined,
                  ApiKeyId: formValues.authMethodUsed === "API Key" ? formValues.apiKeyId : undefined,
                  EncodedApiKey: formValues.authMethodUsed === "Encoded API Key" ? formValues.encodedApiKey : undefined,
              }
            : undefined;

    return {
        ApiKey: apiKey,
        Basic:
            auth === "Basic"
                ? {
                      Username: formValues.username,
                      Password: formValues.password,
                  }
                : null,
        Certificate:
            auth === "Certificate"
                ? {
                      CertificatesBase64: formValues.certificatesBase64,
                  }
                : null,
    };
}

export function mapElasticSearchConnectionStringToDto(connection: ElasticSearchConnection): ConnectionStringDto {
    return {
        Type: "ElasticSearch",
        Name: connection.name,
        Nodes: connection.nodes.map((x) => x.url),
        Authentication: mapElasticSearchAuthenticationToDto(connection),
    };
}

export function mapKafkaConnectionStringToDto(connection: KafkaConnection): ConnectionStringDto {
    return {
        Type: "Queue",
        BrokerType: "Kafka",
        Name: connection.name,
        KafkaConnectionSettings: {
            BootstrapServers: connection.bootstrapServers,
            ConnectionOptions: Object.fromEntries(connection.connectionOptions.map((x) => [x.key, x.value])),
            UseRavenCertificate: connection.isUseRavenCertificate,
        },
    };
}

export function mapRabbitMqStringToDto(connection: RabbitMqConnection): ConnectionStringDto {
    return {
        Type: "Queue",
        BrokerType: "RabbitMq",
        Name: connection.name,
        RabbitMqConnectionSettings: {
            ConnectionString: connection.connectionString,
        },
    };
}
//TODO: map azure

export function mapConnectionStringToDto(connection: Connection): ConnectionStringDto {
    const type = connection.type;

    switch (type) {
        case "Raven":
            return mapRavenConnectionStringToDto(connection);
        case "Sql":
            return mapSqlConnectionStringToDto(connection);
        case "Olap":
            return mapOlapConnectionStringToDto(connection);
        case "ElasticSearch":
            return mapElasticSearchConnectionStringToDto(connection);
        case "Kafka":
            return mapKafkaConnectionStringToDto(connection);
        case "RabbitMQ":
            return mapRabbitMqStringToDto(connection);
        case "AzureQueueStorage":
            throw new Error("NOT IMPLETED"); //TODO:
        default:
            return assertUnreachable(type);
    }
}
