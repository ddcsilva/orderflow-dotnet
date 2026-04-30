using OrderFlow.Orders.Domain.Events;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

public sealed class Order : AggregateRoot
{
    public OrderNumber OrderNumber { get; private init; } = null!;
    public Guid CustomerId { get; private init; }
    public Address ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;
    public string? CancellationReason { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { } // EF Core

    public static Order Create(Guid customerId, Address shippingAddress)
    {
        if (customerId == Guid.Empty)
            throw new OrderDomainException("Customer ID is required.", "INVALID_CUSTOMER");

        ArgumentNullException.ThrowIfNull(shippingAddress);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = OrderNumber.Create(),
            CustomerId = customerId,
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Pending,
            TotalAmount = Money.Zero(),
            CreatedAt = DateTime.UtcNow
        };

        order.AddDomainEvent(new OrderCreatedDomainEvent(
            order.Id, order.OrderNumber.Value, customerId, 0m));

        return order;
    }

    public void AddItem(Guid productId, string productName, Money unitPrice, int quantity)
    {
        EnsurePendingStatus("add items");

        if (productId == Guid.Empty)
            throw new OrderDomainException("Product ID is required.", "INVALID_PRODUCT");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            var item = OrderItem.Create(Id, productId, productName, unitPrice, quantity);
            _items.Add(item);
        }

        RecalculateTotal();
        SetUpdated();

        AddDomainEvent(new OrderItemAddedDomainEvent(Id, productId, quantity));
    }

    public void RemoveItem(Guid productId)
    {
        EnsurePendingStatus("remove items");

        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new OrderDomainException(
                $"Item with product ID '{productId}' not found in order.", "ITEM_NOT_FOUND");

        _items.Remove(item);
        RecalculateTotal();
        SetUpdated();
    }

    public void Confirm()
    {
        if (_items.Count == 0)
            throw new OrderDomainException(
                "Cannot confirm an order with no items.", "EMPTY_ORDER");

        Status = Status.TransitionTo(OrderStatus.Confirmed);
        SetUpdated();

        AddDomainEvent(new OrderConfirmedDomainEvent(Id, OrderNumber.Value, TotalAmount.Amount));
    }

    public void Ship()
    {
        Status = Status.TransitionTo(OrderStatus.Shipped);
        SetUpdated();
    }

    public void Deliver()
    {
        Status = Status.TransitionTo(OrderStatus.Delivered);
        SetUpdated();
    }

    public void Cancel(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = Status.TransitionTo(OrderStatus.Cancelled);
        CancellationReason = reason.Trim();
        SetUpdated();

        AddDomainEvent(new OrderCancelledDomainEvent(Id, OrderNumber.Value, reason));
    }

    public void UpdateShippingAddress(Address newAddress)
    {
        EnsurePendingStatus("update shipping address");

        ArgumentNullException.ThrowIfNull(newAddress);

        ShippingAddress = newAddress;
        SetUpdated();
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Aggregate(
            Money.Zero(),
            (total, item) => total.Add(item.TotalPrice));
    }

    private void EnsurePendingStatus(string action)
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException(
                $"Cannot {action} for an order with status '{Status.Value}'.",
                "INVALID_ORDER_STATUS");
    }
}
