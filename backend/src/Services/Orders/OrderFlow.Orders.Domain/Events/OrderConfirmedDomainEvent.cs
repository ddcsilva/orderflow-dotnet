using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderConfirmedDomainEvent(
    Guid OrderId,
    string OrderNumber,
    decimal TotalAmount,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderConfirmedDomainEvent(Guid orderId, string orderNumber, decimal totalAmount)
        : this(orderId, orderNumber, totalAmount, DateTime.UtcNow) { }
}
