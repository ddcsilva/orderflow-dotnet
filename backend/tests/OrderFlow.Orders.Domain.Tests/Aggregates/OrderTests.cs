using FluentAssertions;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Events;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.ValueObjects;

namespace OrderFlow.Orders.Domain.Tests.Aggregates;

public class OrderTests
{
    private static Address CreateTestAddress() =>
        Address.Create("Rua Exemplo", "123", "Centro", "São Paulo", "SP", "01001-000");

    private static Order CreatePendingOrder()
    {
        var order = Order.Create(Guid.NewGuid(), CreateTestAddress());
        order.ClearDomainEvents();
        return order;
    }

    [Fact]
    public void Create_ValidInput_CreatesOrderWithPendingStatus()
    {
        var customerId = Guid.NewGuid();
        var address = CreateTestAddress();

        var order = Order.Create(customerId, address);

        order.Id.Should().NotBeEmpty();
        order.CustomerId.Should().Be(customerId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Amount.Should().Be(0m);
        order.Items.Should().BeEmpty();
        order.OrderNumber.Value.Should().StartWith("ORD-");
    }

    [Fact]
    public void Create_ValidInput_RaisesOrderCreatedDomainEvent()
    {
        var order = Order.Create(Guid.NewGuid(), CreateTestAddress());

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void Create_EmptyCustomerId_ThrowsOrderDomainException()
    {
        var act = () => Order.Create(Guid.Empty, CreateTestAddress());

        act.Should().Throw<OrderDomainException>();
    }

    [Fact]
    public void AddItem_WhenPending_AddsItemAndUpdatesTotal()
    {
        var order = CreatePendingOrder();
        var unitPrice = Money.FromDecimal(49.99m);

        order.AddItem(Guid.NewGuid(), "Laptop", unitPrice, 2);

        order.Items.Should().HaveCount(1);
        order.TotalAmount.Amount.Should().Be(99.98m);
    }

    [Fact]
    public void AddItem_SameProductTwice_IncreasesQuantity()
    {
        var order = CreatePendingOrder();
        var productId = Guid.NewGuid();
        var unitPrice = Money.FromDecimal(10m);

        order.AddItem(productId, "Mouse", unitPrice, 2);
        order.AddItem(productId, "Mouse", unitPrice, 3);

        order.Items.Should().HaveCount(1);
        order.Items.First().Quantity.Should().Be(5);
        order.TotalAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void AddItem_MultipleProducts_CalculatesTotalCorrectly()
    {
        var order = CreatePendingOrder();

        order.AddItem(Guid.NewGuid(), "Laptop", Money.FromDecimal(2000m), 1);
        order.AddItem(Guid.NewGuid(), "Mouse", Money.FromDecimal(50m), 2);
        order.AddItem(Guid.NewGuid(), "Keyboard", Money.FromDecimal(150m), 1);

        order.Items.Should().HaveCount(3);
        order.TotalAmount.Amount.Should().Be(2250m);
    }

    [Fact]
    public void AddItem_WhenConfirmed_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(10m), 1);
        order.Confirm();

        var act = () => order.AddItem(Guid.NewGuid(), "Another", Money.FromDecimal(20m), 1);

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*Cannot add items*");
    }

    [Fact]
    public void AddItem_RaisesOrderItemAddedDomainEvent()
    {
        var order = CreatePendingOrder();

        order.AddItem(Guid.NewGuid(), "Product", Money.FromDecimal(10m), 1);

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderItemAddedDomainEvent>();
    }

    [Fact]
    public void RemoveItem_ExistingProduct_RemovesAndRecalculates()
    {
        var order = CreatePendingOrder();
        var productId = Guid.NewGuid();
        order.AddItem(productId, "Item", Money.FromDecimal(100m), 1);
        order.AddItem(Guid.NewGuid(), "Other", Money.FromDecimal(50m), 1);

        order.RemoveItem(productId);

        order.Items.Should().HaveCount(1);
        order.TotalAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void RemoveItem_NonExistentProduct_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();

        var act = () => order.RemoveItem(Guid.NewGuid());

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void Confirm_WithItems_ChangesStatusToConfirmed()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_WithoutItems_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();

        var act = () => order.Confirm();

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*no items*");
    }

    [Fact]
    public void Confirm_RaisesOrderConfirmedDomainEvent()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);
        order.ClearDomainEvents();

        order.Confirm();

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderConfirmedDomainEvent>();
    }

    [Fact]
    public void FullLifecycle_PendingToDelivered_Works()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);

        order.Confirm();
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.Ship();
        order.Status.Should().Be(OrderStatus.Shipped);

        order.Deliver();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cancel_PendingOrder_ChangesStatusToCancelled()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);

        order.Cancel("Customer changed their mind");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Customer changed their mind");
    }

    [Fact]
    public void Cancel_DeliveredOrder_ThrowsOrderDomainException()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);
        order.Confirm();
        order.Ship();
        order.Deliver();

        var act = () => order.Cancel("Too late");

        act.Should().Throw<OrderDomainException>();
    }

    [Fact]
    public void Cancel_RaisesOrderCancelledDomainEvent()
    {
        var order = CreatePendingOrder();
        order.AddItem(Guid.NewGuid(), "Item", Money.FromDecimal(100m), 1);
        order.ClearDomainEvents();

        order.Cancel("Reason");

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCancelledDomainEvent>();
    }
}
