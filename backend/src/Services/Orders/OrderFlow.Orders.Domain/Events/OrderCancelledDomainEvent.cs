using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderCancelledDomainEvent(
    Guid OrderId,
    string OrderNumber,
    string Reason,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderCancelledDomainEvent(Guid orderId, string orderNumber, string reason)
        : this(orderId, orderNumber, reason, DateTime.UtcNow) { }
}
