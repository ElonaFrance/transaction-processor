using TransactionProcessor.Application.Services.Contracts;

namespace TransactionProcessor.Application.Services;

public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(ILogger<LoggingService> logger) => _logger = logger;

    public void LogInformation(string message, params object[] args) => _logger.LogInformation(message, args);
    public void LogWarning(string message, params object[] args) => _logger.LogWarning(message, args);
    public void LogWarning(Exception ex, string message, params object[] args) => _logger.LogWarning(ex, message, args);
    public void LogError(Exception ex, string message, params object[] args) => _logger.LogError(ex, message, args);
    public void LogDebug(string message, params object[] args) => _logger.LogDebug(message, args);
}
