using System.Text.Json;
using Confluent.Kafka;
using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services.Hosted;

public class TransactionProcessingHostedService : BackgroundService
{
    private readonly IKafkaConsumerService _consumer;
    private readonly IKafkaProducerService _producer;
    private readonly ITransactionProcessingService _processingService;
    private readonly ILoggingService _logger;
    private readonly IConfiguration _configuration;

    public TransactionProcessingHostedService(
        IKafkaConsumerService consumer,
        IKafkaProducerService producer,
        ITransactionProcessingService processingService,
        ILoggingService logger,
        IConfiguration configuration)
    {
        _consumer = consumer;
        _producer = producer;
        _processingService = processingService;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consume() blocks, so yield first to avoid holding up host startup.
        await Task.Yield();

        var pollIntervalSeconds = _configuration.GetValue("Kafka:PollIntervalSeconds", 10);
        _consumer.Subscribe(AppConstants.Kafka.TransactionEventsTopic);
        _logger.LogInformation("TransactionProcessingHostedService started. Polling every {Seconds}s", pollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            // A full batch means there is likely a backlog, so poll again straight away.
            var batchWasFull = await PollAndProcessAsync(stoppingToken);
            if (!batchWasFull)
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), stoppingToken);
        }
    }

    /// <returns>True when a full batch was processed and committed.</returns>
    private async Task<bool> PollAndProcessAsync(CancellationToken stoppingToken)
    {
        var batchSize = _configuration.GetValue("Kafka:ConsumerBatchSize", 50);
        var windowSeconds = _configuration.GetValue("Kafka:ConsumerWindowSeconds", 5);
        List<ConsumeResult<string, string>> resultList = [];

        try
        {
            var results = await _consumer.ConsumeBatchAsync(batchSize, windowSeconds, stoppingToken);

            resultList = results.ToList();
            if (!resultList.Any()) return false;

            var events = new List<TransactionEvent>();
            foreach (var result in resultList)
            {
                var evt = TryDeserialize(result);
                if (evt != null)
                    events.Add(evt);
                else
                    await DeadLetterRawAsync(result, stoppingToken);
            }

            IReadOnlyList<TransactionEvent> failed = events.Any()
                ? await _processingService.ProcessBatchAsync(events, stoppingToken)
                : [];

            // Shutting down mid-batch: leave offsets uncommitted so the batch is re-delivered.
            // usp_InsertTransaction is idempotent, so already-saved events are not double-counted.
            if (stoppingToken.IsCancellationRequested) return false;

            foreach (var evt in failed)
                await _producer.ProduceAsync(AppConstants.Kafka.DeadLetterTopic, evt.ClientId, evt, stoppingToken);

            if (failed.Count > 0)
                _logger.LogWarning("Published {Count} failed events to {Topic}", failed.Count, AppConstants.Kafka.DeadLetterTopic);

            _consumer.CommitOffsets(resultList);
            _logger.LogInformation("Committed offsets for {Count} messages", resultList.Count);
            return resultList.Count >= batchSize;
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Transient DB failure or DLQ publish failure: re-consume the whole batch on the next poll.
            // Saved events are skipped by usp_InsertTransaction's idempotency check.
            _logger.LogError(ex, "Error during poll-and-process cycle; re-consuming {Count} messages", resultList.Count);
            if (resultList.Any())
                TryRewind(resultList);
            return false;
        }
    }

    private void TryRewind(List<ConsumeResult<string, string>> results)
    {
        try
        {
            _consumer.Rewind(results);
        }
        catch (KafkaException ex)
        {
            // Partitions were revoked; the new owner resumes from the last committed offset, which is before this batch.
            _logger.LogWarning(ex, "Could not rewind consumer; relying on committed offsets for re-delivery");
        }
    }

    private TransactionEvent? TryDeserialize(ConsumeResult<string, string> result)
    {
        try
        {
            return JsonSerializer.Deserialize<TransactionEvent>(result.Message.Value);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize message at offset {Offset}", result.Offset);
            return null;
        }
    }

    private async Task DeadLetterRawAsync(ConsumeResult<string, string> result, CancellationToken stoppingToken)
    {
        var deadLetter = new
        {
            Reason = "Unreadable message",
            result.Topic,
            Partition = result.Partition.Value,
            Offset = result.Offset.Value,
            RawValue = result.Message.Value
        };

        await _producer.ProduceAsync(AppConstants.Kafka.DeadLetterTopic, result.Message.Key ?? string.Empty, deadLetter, stoppingToken);
        _logger.LogWarning("Published unreadable message at offset {Offset} to {Topic}", result.Offset, AppConstants.Kafka.DeadLetterTopic);
    }
}
