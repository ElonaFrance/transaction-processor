using Confluent.Kafka;

namespace TransactionProcessor.Application.Services.Contracts;

public interface IKafkaConsumerService
{
    Task<IEnumerable<ConsumeResult<string, string>>> ConsumeBatchAsync(string topic, int batchSize, int windowSeconds, CancellationToken cancellationToken);
    void CommitOffsets(IEnumerable<ConsumeResult<string, string>> results);
    void Dispose();
}
