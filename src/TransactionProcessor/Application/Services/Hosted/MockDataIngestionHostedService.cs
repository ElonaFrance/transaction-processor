using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services.Hosted;

public class MockDataIngestionHostedService : IHostedService, IDisposable
{
    private readonly IKafkaProducerService _producer;
    private readonly ILoggingService _logger;
    private readonly IConfiguration _configuration;
    private System.Timers.Timer? _timer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly Random _random = Random.Shared;

    public MockDataIngestionHostedService(
        IKafkaProducerService producer,
        ILoggingService logger,
        IConfiguration configuration)
    {
        _producer = producer;
        _logger = logger;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var intervalSeconds = _configuration.GetValue("MockData:IntervalSeconds", 5);
        _timer = new System.Timers.Timer(intervalSeconds * 1000);
        _timer.Elapsed += async (_, _) => await PublishBatchAsync();
        _timer.AutoReset = true;
        _timer.Start();

        _logger.LogInformation("MockDataIngestionHostedService started. Publishing every {Seconds}s", intervalSeconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Stop();
        _producer.Flush(TimeSpan.FromSeconds(5));
        return Task.CompletedTask;
    }

    private async Task PublishBatchAsync()
    {
        if (!await _semaphore.WaitAsync(0)) return;

        try
        {
            var batchSize = _configuration.GetValue("MockData:BatchSize", 5);
            var events = GenerateBatch(batchSize);

            foreach (var evt in events)
            {
                await _producer.ProduceAsync(
                    AppConstants.Kafka.TransactionEventsTopic,
                    evt.ClientId,
                    evt);
            }

            _logger.LogInformation("Published {Count} mock transaction events to Kafka", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing mock data batch");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static List<TransactionEvent> GenerateBatch(int size)
    {
        var events = new List<TransactionEvent>(size);
        var sources = new[] { "BankAccount", "CreditCard", "Investment" };

        for (int i = 0; i < size; i++)
        {
            var source = sources[_random.Next(sources.Length)];
            var clientId = AppConstants.MockData.ClientIds[_random.Next(AppConstants.MockData.ClientIds.Length)];
            events.Add(GenerateEvent(source, clientId));
        }

        return events;
    }

    private static TransactionEvent GenerateEvent(string source, string clientId)
    {
        var (merchants, type, minAmount, maxAmount) = source switch
        {
            "BankAccount" => (AppConstants.MockData.BankMerchants, GetBankType(), 50m, 50000m),
            "CreditCard" => (AppConstants.MockData.CreditCardMerchants, "Debit", 20m, 5000m),
            "Investment" => (AppConstants.MockData.InvestmentMerchants, GetInvestmentType(), 500m, 100000m),
            _ => (AppConstants.MockData.BankMerchants, "Debit", 50m, 1000m)
        };

        var merchant = merchants[_random.Next(merchants.Length)];
        var amount = Math.Round((decimal)(_random.NextDouble() * (double)(maxAmount - minAmount) + (double)minAmount), 2);

        return new TransactionEvent
        {
            TransactionId = Guid.NewGuid().ToString(),
            ClientId = clientId,
            SourceId = $"{source.ToUpper()}-{clientId}",
            SourceType = source,
            TransactionType = type,
            Amount = amount,
            Currency = "ZAR",
            Description = merchant,
            MerchantName = merchant,
            TransactionDate = DateTime.UtcNow.AddMinutes(-_random.Next(0, 60)),
            PublishedAt = DateTime.UtcNow
        };
    }

    private static string GetBankType()
    {
        var merchant = AppConstants.MockData.BankMerchants[Random.Shared.Next(AppConstants.MockData.BankMerchants.Length)];
        return merchant.Contains("SALARY") || merchant.Contains("WAGES") ? "Credit" : "Debit";
    }

    private static string GetInvestmentType() =>
        Random.Shared.Next(2) == 0 ? "Debit" : "Credit";

    public void Dispose()
    {
        _timer?.Dispose();
        _semaphore.Dispose();
    }
}
