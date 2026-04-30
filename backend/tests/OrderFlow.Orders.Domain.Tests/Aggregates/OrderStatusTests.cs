using FluentAssertions;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Exceptions;

namespace OrderFlow.Orders.Domain.Tests.Aggregates;

public class OrderStatusTests
{
    [Theory]
    [InlineData("Pending", "Confirmed", true)]
    [InlineData("Pending", "Cancelled", true)]
    [InlineData("Confirmed", "Shipped", true)]
    [InlineData("Confirmed", "Cancelled", true)]
    [InlineData("Shipped", "Delivered", true)]
    [InlineData("Pending", "Shipped", false)]
    [InlineData("Pending", "Delivered", false)]
    [InlineData("Shipped", "Cancelled", false)]
    [InlineData("Delivered", "Pending", false)]
    [InlineData("Cancelled", "Pending", false)]
    public void CanTransitionTo_ValidatesCorrectly(
        string from, string to, bool expectedResult)
    {
        var fromStatus = OrderStatus.FromString(from);
        var toStatus = OrderStatus.FromString(to);

        fromStatus.CanTransitionTo(toStatus).Should().Be(expectedResult);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_ThrowsOrderDomainException()
    {
        var status = OrderStatus.Pending;

        var act = () => status.TransitionTo(OrderStatus.Delivered);

        act.Should().Throw<OrderDomainException>()
            .WithMessage("*Invalid status transition*");
    }

    [Fact]
    public void IsFinal_DeliveredAndCancelled_ReturnsTrue()
    {
        OrderStatus.Delivered.IsFinal.Should().BeTrue();
        OrderStatus.Cancelled.IsFinal.Should().BeTrue();
    }

    [Fact]
    public void IsFinal_NonFinalStatuses_ReturnsFalse()
    {
        OrderStatus.Pending.IsFinal.Should().BeFalse();
        OrderStatus.Confirmed.IsFinal.Should().BeFalse();
        OrderStatus.Shipped.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void FromString_UnknownStatus_ThrowsOrderDomainException()
    {
        var act = () => OrderStatus.FromString("Unknown");

        act.Should().Throw<OrderDomainException>();
    }
}
