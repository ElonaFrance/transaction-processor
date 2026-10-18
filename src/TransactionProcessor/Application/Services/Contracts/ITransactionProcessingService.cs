using TransactionProcessor.Application.Domain.Events;

namespace TransactionProcessor.Application.Services.Contracts;

public interface ITransactionProcessingService
{
    /// <returns>The events that failed permanently (invalid, or a non-transient error) and should be dead-lettered.</returns>
    /// <exception cref="Exception">A transient error that outlasted the retries; the batch should be re-consumed, not dead-lettered.</exception>
    Task<IReadOnlyList<TransactionEvent>> ProcessBatchAsync(IEnumerable<TransactionEvent> events, CancellationToken cancellationToken);
}
