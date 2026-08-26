using Polly;
using Polly.Retry;
using TransactionProcessor.Application.Domain.Entities;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Repositories.Contracts;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services;

public class TransactionProcessingService : ITransactionProcessingService
{
    private readonly ICategoryService _categoryService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAggregationRepository _aggregationRepository;
    private readonly ILoggingService _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public TransactionProcessingService(
        ICategoryService categoryService,
        ITransactionRepository transactionRepository,
        IAggregationRepository aggregationRepository,
        ILoggingService logger)
    {
        _categoryService = categoryService;
        _transactionRepository = transactionRepository;
        _aggregationRepository = aggregationRepository;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<Exception>(ex => IsTransient(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50)),
                onRetry: (ex, delay, attempt, _) =>
                    _logger.LogWarning("Retry {Attempt} after {Delay}ms due to: {Message}", attempt, delay.TotalMilliseconds, ex.Message));
    }

    public async Task ProcessBatchAsync(IEnumerable<TransactionEvent> events, CancellationToken cancellationToken)
    {
        var processedCount = 0;
        var failedCount = 0;

        foreach (var txEvent in events)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                    await ProcessSingleAsync(txEvent, cancellationToken));

                processedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(ex, "Failed to process transaction {TransactionId} after retries", txEvent.TransactionId);
            }
        }

        _logger.LogInformation("Batch complete. Processed: {Processed}, Failed: {Failed}", processedCount, failedCount);
    }

    private async Task ProcessSingleAsync(TransactionEvent txEvent, CancellationToken cancellationToken)
    {
        var (categoryId, categoryName) = _categoryService.Categorise(txEvent);

        var transaction = new TransactionEntity
        {
            Id = txEvent.TransactionId,
            ClientId = txEvent.ClientId,
            SourceId = txEvent.SourceId,
            SourceType = txEvent.SourceType,
            TransactionType = txEvent.TransactionType,
            Amount = txEvent.Amount,
            Currency = txEvent.Currency,
            CategoryId = categoryId,
            CategoryName = categoryName,
            Description = txEvent.Description,
            MerchantName = txEvent.MerchantName,
            TransactionDate = txEvent.TransactionDate,
            ProcessedDate = DateTime.UtcNow,
            Status = "PROCESSED"
        };

        await _transactionRepository.InsertAsync(transaction, cancellationToken);
        await UpdateAggregationsAsync(transaction, cancellationToken);
    }

    private async Task UpdateAggregationsAsync(TransactionEntity transaction, CancellationToken cancellationToken)
    {
        var isDebit = transaction.TransactionType.Equals("Debit", StringComparison.OrdinalIgnoreCase);
        var monthKey = transaction.TransactionDate.ToString("yyyy-MM");

        var allTime = new AggregationEntity
        {
            ClientId = transaction.ClientId,
            Period = "ALL_TIME",
            PeriodKey = "ALL",
            TotalDebit = isDebit ? transaction.Amount : 0,
            TotalCredit = isDebit ? 0 : transaction.Amount,
            TransactionCount = 1,
            LastUpdated = DateTime.UtcNow
        };

        var monthly = new AggregationEntity
        {
            ClientId = transaction.ClientId,
            Period = "MONTHLY",
            PeriodKey = monthKey,
            TotalDebit = isDebit ? transaction.Amount : 0,
            TotalCredit = isDebit ? 0 : transaction.Amount,
            TransactionCount = 1,
            LastUpdated = DateTime.UtcNow
        };

        await _aggregationRepository.UpsertAsync(allTime, cancellationToken);
        await _aggregationRepository.UpsertAsync(monthly, cancellationToken);
    }

    private static bool IsTransient(Exception ex) =>
        ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("lock wait timeout", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("gone away", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
}
