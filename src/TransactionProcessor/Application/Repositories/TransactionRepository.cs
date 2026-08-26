using System.Text.Json;
using Dapper;
using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Entities;
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
        var payload = JsonSerializer.Serialize(transaction);

        var result = await connection.QueryFirstOrDefaultAsync<ProcResult>(
            AppConstants.StoredProcedures.InsertTransaction,
            new { Payload = payload },
            commandType: System.Data.CommandType.StoredProcedure);

        if (result?.ErrorCode != 0)
            throw new InvalidOperationException($"InsertTransaction failed: [{result?.ErrorCode}] {result?.ErrorMessage}");
    }

    private record ProcResult(int ErrorCode, string? ErrorMessage);
}
