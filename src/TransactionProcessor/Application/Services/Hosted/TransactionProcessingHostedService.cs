using System.Text.Json;
using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services.Hosted;

public class TransactionProcessingHostedService : IHostedService, IDisposable
{
    private readonly IKafkaConsumerService _consumer;
    private readonly ITransactionProcessingService _processingService;
    private readonly ILoggingService _logger;
    private readonly IConfiguration _configuration;
    private System.Timers.Timer? _timer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private CancellationToken _cancellationToken;

    public TransactionProcessingHostedService(
        IKafkaConsumerService consumer,
        ITransactionProcessingService processingService,
        ILoggingService logger,
        IConfiguration configuration)
    {
        _consumer = consumer;
        _processingService = processingService;
        _logger = logger;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        var pollIntervalSeconds = _configuration.GetValue("Kafka:PollIntervalSeconds", 10);

        _timer = new System.Timers.Timer(pollIntervalSeconds * 1000);
        _timer.Elapsed += async (_, _) => await PollAndProcessAsync();
        _timer.AutoReset = true;
        _timer.Start();

        _logger.LogInformation("TransactionProcessingHostedService started. Polling every {Seconds}s", pollIntervalSeconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Stop();
        return Task.CompletedTask;
    }

    private async Task PollAndProcessAsync()
    {
        if (!await _semaphore.WaitAsync(0)) return;

        try
        {
            var batchSize = _configuration.GetValue("Kafka:ConsumerBatchSize", 50);
            var windowSeconds = _configuration.GetValue("Kafka:ConsumerWindowSeconds", 5);

            var results = await _consumer.ConsumeBatchAsync(
                AppConstants.Kafka.TransactionEventsTopic,
                batchSize,
                windowSeconds,
                _cancellationToken);

            var resultList = results.ToList();
            if (!resultList.Any()) return;

            var events = new List<TransactionEvent>();
            foreach (var result in resultList)
            {
                try
                {
                    var evt = JsonSerializer.Deserialize<TransactionEvent>(result.Message.Value);
                    if (evt != null) events.Add(evt);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize message at offset {Offset}", result.Offset);
                }
            }

            if (events.Any())
                await _processingService.ProcessBatchAsync(events, _cancellationToken);

            _consumer.CommitOffsets(resultList);
            _logger.LogInformation("Committed offsets for {Count} messages", resultList.Count);
        }
        catch (Exception ex) when (!_cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error during poll-and-process cycle");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _semaphore.Dispose();
        _consumer?.Dispose();
    }
}
