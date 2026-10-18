using MySqlConnector;
using Polly;
using Polly.Retry;
using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Entities;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Domain.Exceptions;
using TransactionProcessor.Application.Repositories.Contracts;
using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services;

public class TransactionProcessingService : ITransactionProcessingService
{
    private readonly ICategoryService _categoryService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILoggingService _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public TransactionProcessingService(
        ICategoryService categoryService,
        ITransactionRepository transactionRepository,
        ILoggingService logger)
    {
        _categoryService = categoryService;
        _transactionRepository = transactionRepository;
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

    public async Task<IReadOnlyList<TransactionEvent>> ProcessBatchAsync(IEnumerable<TransactionEvent> events, CancellationToken cancellationToken)
    {
        var processedCount = 0;
        var failed = new List<TransactionEvent>();

        foreach (var txEvent in events)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (string.IsNullOrWhiteSpace(txEvent.TransactionId) || string.IsNullOrWhiteSpace(txEvent.ClientId))
            {
                failed.Add(txEvent);
                _logger.LogWarning("Event {EventId} is missing TransactionId or ClientId", txEvent.EventId);
                continue;
            }

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                    await ProcessSingleAsync(txEvent, cancellationToken));

                processedCount++;
            }
            // Transient errors that outlast the retries (e.g. DB down) propagate so the caller re-consumes
            // the batch instead of dead-lettering events that would succeed later.
            catch (Exception ex) when (!IsTransient(ex) && ex is not OperationCanceledException)
            {
                failed.Add(txEvent);
                _logger.LogError(ex, "Failed to process transaction {TransactionId}", txEvent.TransactionId);
            }
        }

        _logger.LogInformation("Batch complete. Processed: {Processed}, Failed: {Failed}", processedCount, failed.Count);
        return failed;
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

        // usp_InsertTransaction also updates the ALL_TIME + MONTHLY aggregations atomically.
        await _transactionRepository.InsertAsync(transaction, cancellationToken);
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        MySqlException mySqlException => mySqlException.IsTransient,
        StoredProcedureException procException => AppConstants.MySql.TransientErrorCodes.Contains(procException.ErrorCode),
        TimeoutException => true,
        _ => false
    };
}
