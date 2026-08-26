using System.Text.Json;
using Dapper;
using TransactionProcessor.Application.Domain.Constants;
using TransactionProcessor.Application.Domain.Entities;
using TransactionProcessor.Application.Repositories.Contracts;
using TransactionProcessor.Application.Services;

namespace TransactionProcessor.Application.Repositories;

public class AggregationRepository : IAggregationRepository
{
    private readonly DapperService _dapperService;
    private readonly ILogger<AggregationRepository> _logger;

    public AggregationRepository(DapperService dapperService, ILogger<AggregationRepository> logger)
    {
        _dapperService = dapperService;
        _logger = logger;
    }

    public async Task UpsertAsync(AggregationEntity aggregation, CancellationToken cancellationToken = default)
    {
        using var connection = _dapperService.CreateConnection();
        var payload = JsonSerializer.Serialize(aggregation);

        var result = await connection.QueryFirstOrDefaultAsync<ProcResult>(
            AppConstants.StoredProcedures.UpsertAggregation,
            new { Payload = payload },
            commandType: System.Data.CommandType.StoredProcedure);

        if (result?.ErrorCode != 0)
            throw new InvalidOperationException($"UpsertAggregation failed: [{result?.ErrorCode}] {result?.ErrorMessage}");
    }

    private record ProcResult(int ErrorCode, string? ErrorMessage);
}
