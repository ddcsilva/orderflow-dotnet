using FluentAssertions;
using OrderFlow.Orders.Domain.ValueObjects;

namespace OrderFlow.Orders.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void FromDecimal_ValidAmount_CreatesMoney()
    {
        var money = Money.FromDecimal(100.50m);

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("BRL");
    }

    [Fact]
    public void FromDecimal_NegativeAmount_ThrowsArgumentException()
    {
        var act = () => Money.FromDecimal(-10m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromDecimal_RoundsToTwoDecimals()
    {
        var money = Money.FromDecimal(100.555m);

        money.Amount.Should().Be(100.56m);
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSummedMoney()
    {
        var a = Money.FromDecimal(100m);
        var b = Money.FromDecimal(50m);

        var result = a.Add(b);

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("BRL");
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsInvalidOperation()
    {
        var brl = Money.FromDecimal(100m, "BRL");
        var usd = Money.FromDecimal(50m, "USD");

        var act = () => brl.Add(usd);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Multiply_ByQuantity_ReturnsCorrectAmount()
    {
        var unitPrice = Money.FromDecimal(49.99m);

        var total = unitPrice.Multiply(3);

        total.Amount.Should().Be(149.97m);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = Money.FromDecimal(100m, "BRL");
        var b = Money.FromDecimal(100m, "BRL");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = Money.FromDecimal(100m);
        var b = Money.FromDecimal(200m);

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Zero_ReturnsZeroAmount()
    {
        var zero = Money.Zero();

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("BRL");
    }
}
