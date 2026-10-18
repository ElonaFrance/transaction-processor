using Confluent.Kafka;

namespace TransactionProcessor.Application.Services.Contracts;

public interface IKafkaConsumerService
{
    void Subscribe(string topic);
    Task<IEnumerable<ConsumeResult<string, string>>> ConsumeBatchAsync(int batchSize, int windowSeconds, CancellationToken cancellationToken);
    void CommitOffsets(IEnumerable<ConsumeResult<string, string>> results);
    void Rewind(IEnumerable<ConsumeResult<string, string>> results);
    void Dispose();
}
