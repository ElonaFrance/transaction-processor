using TransactionProcessor.Application.Domain.Entities;

namespace TransactionProcessor.Application.Repositories.Contracts;

public interface ITransactionRepository
{
    Task InsertAsync(TransactionEntity transaction, CancellationToken cancellationToken = default);
}
