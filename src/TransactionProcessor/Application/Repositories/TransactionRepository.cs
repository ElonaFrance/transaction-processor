using Dapper;
using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Entities;
using TransactionProcessor.Application.Domain.Exceptions;
using TransactionProcessor.Application.Repositories.Contracts;
using TransactionProcessor.Application.Services;

namespace TransactionProcessor.Application.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly DapperService _dapperService;
    private readonly ILogger<TransactionRepository> _logger;

    public TransactionRepository(DapperService dapperService, ILogger<TransactionRepository> logger)
    {
        _dapperService = dapperService;
        _logger = logger;
    }

    public async Task InsertAsync(TransactionEntity transaction, CancellationToken cancellationToken = default)
    {
        using var connection = _dapperService.CreateConnection();
        var payload = DapperService.SerializePayload(transaction);

        var result = await connection.QueryFirstOrDefaultAsync<ProcResult>(
            AppConstants.StoredProcedures.InsertTransaction,
            new { Payload = payload },
            commandType: System.Data.CommandType.StoredProcedure);

        if (result == null || result.ErrorCode != 0)
            throw new StoredProcedureException(AppConstants.StoredProcedures.InsertTransaction, result?.ErrorCode ?? -1, result?.ErrorMessage);
    }

    private class ProcResult
    {
        public int ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
