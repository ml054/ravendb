﻿using System;
using System.Collections.Generic;
using Raven.Client.Documents.Operations.ConnectionStrings;
using Sparrow.Json.Parsing;

namespace Raven.Client.Documents.Operations.ETL.Queue;

public sealed class QueueConnectionString : ConnectionString
{
    public QueueBrokerType BrokerType { get; set; }

    public KafkaConnectionSettings KafkaConnectionSettings { get; set; }

    public RabbitMqConnectionSettings RabbitMqConnectionSettings { get; set; }

    public AzureQueueStorageConnectionSettings AzureQueueStorageConnectionSettings { get; set; }
    
    public override ConnectionStringType Type => ConnectionStringType.Queue;

    protected override void ValidateImpl(ref List<string> errors)
    {
        switch (BrokerType)
        {
            case QueueBrokerType.Kafka:
                if (KafkaConnectionSettings == null || string.IsNullOrWhiteSpace(KafkaConnectionSettings.BootstrapServers))
                {
                    errors.Add($"{nameof(KafkaConnectionSettings)} has no valid setting.");
                }
                break;
            case QueueBrokerType.RabbitMq:
                if (RabbitMqConnectionSettings == null || string.IsNullOrWhiteSpace(RabbitMqConnectionSettings.ConnectionString))
                {
                    errors.Add($"{nameof(RabbitMqConnectionSettings)} has no valid setting.");
                }
                break;
            case QueueBrokerType.AzureQueueStorage:
                if (AzureQueueStorageConnectionSettings?.Authentication == null)
                {
                    errors.Add($"{nameof(AzureQueueStorageConnectionSettings)} has no valid setting.");
                }
                break;
            default:
                throw new NotSupportedException($"'{BrokerType}' broker is not supported");
        }
    }

    internal string GetUrl()
    {
        string url;

        switch (BrokerType)
        {
            case QueueBrokerType.Kafka:
                url = KafkaConnectionSettings.BootstrapServers;
                break;
            case QueueBrokerType.RabbitMq:
                var connectionString = RabbitMqConnectionSettings.ConnectionString;

                int indexOfStartServerUri = connectionString.IndexOf("@", StringComparison.OrdinalIgnoreCase);

                url = indexOfStartServerUri != -1 ? connectionString.Substring(indexOfStartServerUri + 1) : null;
                break;
            case QueueBrokerType.AzureQueueStorage:
                url = AzureQueueStorageConnectionSettings.GetStorageUrl();
                break;
            default:
                throw new NotSupportedException($"'{BrokerType}' broker is not supported");
        }

        return url;
    }
    
    public override DynamicJsonValue ToJson()
    {
        var json = base.ToJson();

        json[nameof(BrokerType)] = BrokerType;
        json[nameof(KafkaConnectionSettings)] = KafkaConnectionSettings?.ToJson();
        json[nameof(RabbitMqConnectionSettings)] = RabbitMqConnectionSettings?.ToJson();
        json[nameof(AzureQueueStorageConnectionSettings)] = AzureQueueStorageConnectionSettings?.ToJson();

        return json;
    }

    public override DynamicJsonValue ToAuditJson()
    {
        return ToJson();
    }
}
