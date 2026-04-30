namespace OrderFlow.Contracts.IntegrationEvents;

public sealed record OrderCreated
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime OccurredOn { get; init; }
}
