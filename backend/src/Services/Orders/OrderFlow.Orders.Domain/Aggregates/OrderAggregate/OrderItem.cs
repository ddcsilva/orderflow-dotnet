using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

public sealed class OrderItem : Entity
{
    public Guid OrderId { get; private init; }
    public Guid ProductId { get; private init; }
    public string ProductName { get; private init; } = string.Empty;
    public Money UnitPrice { get; private init; } = null!;
    public int Quantity { get; private set; }

    public Money TotalPrice => UnitPrice.Multiply(Quantity);

    private OrderItem() { } // EF Core

    internal static OrderItem Create(
        Guid orderId,
        Guid productId,
        string productName,
        Money unitPrice,
        int quantity)
    {
        if (quantity <= 0)
            throw new OrderDomainException("Quantity must be at least 1.", "INVALID_QUANTITY");

        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentNullException.ThrowIfNull(unitPrice);

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }

    internal void IncreaseQuantity(int additionalQuantity)
    {
        if (additionalQuantity <= 0)
            throw new OrderDomainException("Additional quantity must be positive.", "INVALID_QUANTITY");

        Quantity += additionalQuantity;
    }

    internal void DecreaseQuantity(int reduceBy)
    {
        if (reduceBy <= 0)
            throw new OrderDomainException("Reduce quantity must be positive.", "INVALID_QUANTITY");

        if (Quantity - reduceBy < 1)
            throw new OrderDomainException(
                "Cannot reduce quantity below 1. Remove the item instead.", "INVALID_QUANTITY");

        Quantity -= reduceBy;
    }
}
