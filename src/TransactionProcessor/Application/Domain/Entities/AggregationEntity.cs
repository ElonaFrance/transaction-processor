namespace TransactionProcessor.Application.Domain.Entities;

public class AggregationEntity
{
    public string ClientId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string PeriodKey { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int TransactionCount { get; set; }
    public DateTime LastUpdated { get; set; }
}
