using TransactionProcessor.Application.Repositories;
using TransactionProcessor.Application.Repositories.Contracts;
using TransactionProcessor.Application.Services;
using TransactionProcessor.Application.Services.Contracts;
using TransactionProcessor.Application.Services.Hosted;

namespace TransactionProcessor.Core.Extensions;

public static class ServiceCollectionExtensions
{
    // All singletons: the hosted workers are singletons, and none of these hold per-request state.
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<DapperService>();
        services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
        services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<ITransactionProcessingService, TransactionProcessingService>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        return services;
    }

    public static IServiceCollection AddHostedWorkers(this IServiceCollection services)
    {
        services.AddHostedService<MockDataIngestionHostedService>();
        services.AddHostedService<TransactionProcessingHostedService>();
        return services;
    }
}
