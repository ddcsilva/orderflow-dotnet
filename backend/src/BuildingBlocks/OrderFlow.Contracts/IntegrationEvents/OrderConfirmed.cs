namespace OrderFlow.Contracts.IntegrationEvents;

public sealed record OrderConfirmed
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime OccurredOn { get; init; }
}
