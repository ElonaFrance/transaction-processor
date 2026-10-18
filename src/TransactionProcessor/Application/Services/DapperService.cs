using System.Text.Json;
using System.Text.Json.Serialization;
using MySqlConnector;
using TransactionProcessor.Core.Converters;

namespace TransactionProcessor.Application.Services;

public class DapperService
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new MySqlDateTimeConverter() }
    };

    private readonly string _connectionString;

    public DapperService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Database connection string is not configured.");
    }

    public MySqlConnection CreateConnection() => new(_connectionString);

    public static string SerializePayload<T>(T payload) => JsonSerializer.Serialize(payload, PayloadOptions);
}
