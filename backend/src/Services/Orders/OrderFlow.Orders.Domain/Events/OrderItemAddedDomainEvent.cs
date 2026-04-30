using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderItemAddedDomainEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderItemAddedDomainEvent(Guid orderId, Guid productId, int quantity)
        : this(orderId, productId, quantity, DateTime.UtcNow) { }
}
