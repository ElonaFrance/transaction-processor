namespace TransactionProcessor.Application.Domain.Events;

public class TransactionEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string TransactionId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string Description { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}
