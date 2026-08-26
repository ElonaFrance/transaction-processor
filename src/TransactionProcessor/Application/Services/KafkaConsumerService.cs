using Confluent.Kafka;
using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services;

public class KafkaConsumerService : IKafkaConsumerService, IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaConsumerService> _logger;
    private bool _disposed;

    public KafkaConsumerService(IConfiguration configuration, ILogger<KafkaConsumerService> logger)
    {
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured"),
            GroupId = AppConstants.Kafka.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = true,
            SessionTimeoutMs = 30000
        };

        ApplySaslIfConfigured(config, configuration);
        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public async Task<IEnumerable<ConsumeResult<string, string>>> ConsumeBatchAsync(
        string topic, int batchSize, int windowSeconds, CancellationToken cancellationToken)
    {
        _consumer.Subscribe(topic);
        var batch = new List<ConsumeResult<string, string>>();
        var deadline = DateTime.UtcNow.AddSeconds(windowSeconds);

        while (batch.Count < batchSize && DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (result == null || result.IsPartitionEOF) break;
                batch.Add(result);
            }
            catch (ConsumeException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Consume error on {Topic}", topic);
                await Task.Delay(500, cancellationToken);
            }
        }

        return batch;
    }

    public void CommitOffsets(IEnumerable<ConsumeResult<string, string>> results)
    {
        foreach (var result in results)
            _consumer.Commit(result);
    }

    private static void ApplySaslIfConfigured(ConsumerConfig config, IConfiguration configuration)
    {
        var saslUsername = configuration["Kafka:SaslUsername"];
        if (string.IsNullOrWhiteSpace(saslUsername)) return;

        config.SecurityProtocol = SecurityProtocol.SaslSsl;
        config.SaslMechanism = SaslMechanism.ScramSha512;
        config.SaslUsername = saslUsername;
        config.SaslPassword = configuration["Kafka:SaslPassword"];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _consumer?.Close();
        _consumer?.Dispose();
        _disposed = true;
    }
}
