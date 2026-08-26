using System.Text.Json;
using Confluent.Kafka;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services;

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured"),
            CompressionType = CompressionType.Lz4,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        ApplySaslIfConfigured(config, configuration);
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);
        var kafkaMessage = new Message<string, string> { Key = key, Value = json };

        var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
        _logger.LogDebug("Produced to {Topic} partition {Partition} offset {Offset}", topic, result.Partition, result.Offset);
    }

    public void Flush(TimeSpan timeout) => _producer.Flush(timeout);

    private static void ApplySaslIfConfigured(ProducerConfig config, IConfiguration configuration)
    {
        var saslUsername = configuration["Kafka:SaslUsername"];
        if (string.IsNullOrWhiteSpace(saslUsername)) return;

        config.SecurityProtocol = SecurityProtocol.SaslSsl;
        config.SaslMechanism = SaslMechanism.ScramSha512;
        config.SaslUsername = saslUsername;
        config.SaslPassword = configuration["Kafka:SaslPassword"];
    }

    public void Dispose() => _producer?.Dispose();
}
