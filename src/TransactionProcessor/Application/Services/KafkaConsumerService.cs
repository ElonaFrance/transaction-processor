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
            SessionTimeoutMs = 30000
        };

        ApplySaslIfConfigured(config, configuration);
        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public void Subscribe(string topic) => _consumer.Subscribe(topic);

    public async Task<IEnumerable<ConsumeResult<string, string>>> ConsumeBatchAsync(
        int batchSize, int windowSeconds, CancellationToken cancellationToken)
    {
        var batch = new List<ConsumeResult<string, string>>();
        var deadline = DateTime.UtcNow.AddSeconds(windowSeconds);

        while (batch.Count < batchSize && DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (result == null) continue;
                batch.Add(result);
            }
            catch (ConsumeException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Consume error on {Topic}", ex.ConsumerRecord?.Topic);
                await Task.Delay(500, cancellationToken);
            }
        }

        return batch;
    }

    // Commits the next offset to read for each partition in the batch, in one call.
    public void CommitOffsets(IEnumerable<ConsumeResult<string, string>> results)
    {
        var offsets = results
            .GroupBy(r => r.TopicPartition)
            .Select(g => new TopicPartitionOffset(g.Key, g.Max(r => r.Offset.Value) + 1))
            .ToList();

        if (offsets.Count > 0)
            _consumer.Commit(offsets);
    }

    // Moves each partition back to the first offset in the batch so it is consumed again on the next poll.
    // Without this, skipping a commit is not enough: the consumer's in-memory position has already moved on,
    // and the next successful commit would skip past the unprocessed messages.
    public void Rewind(IEnumerable<ConsumeResult<string, string>> results)
    {
        foreach (var partition in results.GroupBy(r => r.TopicPartition))
            _consumer.Seek(new TopicPartitionOffset(partition.Key, partition.Min(r => r.Offset.Value)));
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
