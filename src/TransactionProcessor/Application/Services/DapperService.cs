using MySqlConnector;

namespace TransactionProcessor.Application.Services;

public class DapperService
{
    private readonly string _connectionString;

    public DapperService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Database connection string is not configured.");
    }

    public MySqlConnection CreateConnection() => new(_connectionString);
}
