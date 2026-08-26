using TransactionProcessor.Application.Repositories;
using TransactionProcessor.Application.Repositories.Contracts;
using TransactionProcessor.Application.Services;
using TransactionProcessor.Application.Services.Contracts;
using TransactionProcessor.Application.Services.Hosted;

namespace TransactionProcessor.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<DapperService>();
        services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
        services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();
        services.AddScoped<ILoggingService, LoggingService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionProcessingService, TransactionProcessingService>();
        services.AddTransient<ITransactionRepository, TransactionRepository>();
        services.AddTransient<IAggregationRepository, AggregationRepository>();
        return services;
    }

    public static IServiceCollection AddHostedWorkers(this IServiceCollection services)
    {
        services.AddHostedService<MockDataIngestionHostedService>();
        services.AddHostedService<TransactionProcessingHostedService>();
        return services;
    }
}
