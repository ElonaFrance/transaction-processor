namespace TransactionProcessor.Application.Services.Contracts;

public interface IKafkaProducerService
{
    Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
    void Flush(TimeSpan timeout);
}
