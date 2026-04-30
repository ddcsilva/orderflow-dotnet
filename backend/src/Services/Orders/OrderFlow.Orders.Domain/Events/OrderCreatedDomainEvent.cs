using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Events;

public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime OccurredOn) : IDomainEvent
{
    public OrderCreatedDomainEvent(Guid orderId, string orderNumber, Guid customerId, decimal totalAmount)
        : this(orderId, orderNumber, customerId, totalAmount, DateTime.UtcNow) { }
}
