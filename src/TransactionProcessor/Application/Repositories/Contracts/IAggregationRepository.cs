using TransactionProcessor.Application.Domain.Entities;

namespace TransactionProcessor.Application.Repositories.Contracts;

public interface IAggregationRepository
{
    Task UpsertAsync(AggregationEntity aggregation, CancellationToken cancellationToken = default);
}
