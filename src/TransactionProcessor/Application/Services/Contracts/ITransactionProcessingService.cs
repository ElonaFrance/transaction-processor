using TransactionProcessor.Application.Domain.Events;

namespace TransactionProcessor.Application.Services.Contracts;

public interface ITransactionProcessingService
{
    Task ProcessBatchAsync(IEnumerable<TransactionEvent> events, CancellationToken cancellationToken);
}
