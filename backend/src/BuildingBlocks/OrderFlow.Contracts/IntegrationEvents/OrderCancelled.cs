namespace OrderFlow.Contracts.IntegrationEvents;

public sealed record OrderCancelled
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime OccurredOn { get; init; }
}
