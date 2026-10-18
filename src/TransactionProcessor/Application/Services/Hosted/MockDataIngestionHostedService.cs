using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services.Hosted;

public class MockDataIngestionHostedService : BackgroundService
{
    private readonly IKafkaProducerService _producer;
    private readonly ILoggingService _logger;
    private readonly IConfiguration _configuration;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _configuration.GetValue("MockData:IntervalSeconds", 5);
        _logger.LogInformation("MockDataIngestionHostedService started. Publishing every {Seconds}s", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            await PublishBatchAsync(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        _producer.Flush(TimeSpan.FromSeconds(5));
    }

    private async Task PublishBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            var batchSize = _configuration.GetValue("MockData:BatchSize", 5);
            var events = GenerateBatch(batchSize);

            foreach (var evt in events)
            {
                await _producer.ProduceAsync(
                    AppConstants.Kafka.TransactionEventsTopic,
                    evt.ClientId,
                    evt,
                    stoppingToken);
            }

            _logger.LogInformation("Published {Count} mock transaction events to Kafka", events.Count);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error publishing mock data batch");
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
        var (merchants, minAmount, maxAmount) = source switch
        {
            "BankAccount" => (AppConstants.MockData.BankMerchants, 50m, 50000m),
            "CreditCard" => (AppConstants.MockData.CreditCardMerchants, 20m, 5000m),
            "Investment" => (AppConstants.MockData.InvestmentMerchants, 500m, 100000m),
            _ => (AppConstants.MockData.BankMerchants, 50m, 1000m)
        };

        var merchant = merchants[_random.Next(merchants.Length)];
        var type = source switch
        {
            "BankAccount" => GetBankType(merchant),
            "Investment" => GetInvestmentType(),
            _ => "Debit"
        };
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

    private static string GetBankType(string merchant) =>
        merchant.Contains("SALARY") || merchant.Contains("WAGES") ? "Credit" : "Debit";

    private static string GetInvestmentType() =>
        Random.Shared.Next(2) == 0 ? "Debit" : "Credit";
}
