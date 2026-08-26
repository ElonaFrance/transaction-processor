using TransactionProcessor.Application.Domain.Events;

namespace TransactionProcessor.Application.Services.Contracts;

public interface ICategoryService
{
    (int CategoryId, string CategoryName) Categorise(TransactionEvent transactionEvent);
}
