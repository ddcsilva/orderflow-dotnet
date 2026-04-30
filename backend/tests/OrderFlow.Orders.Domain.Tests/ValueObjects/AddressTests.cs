using FluentAssertions;
using OrderFlow.Orders.Domain.ValueObjects;

namespace OrderFlow.Orders.Domain.Tests.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Create_ValidInput_CreatesAddress()
    {
        var address = Address.Create(
            "Rua das Flores", "100", "Jardim Primavera",
            "São Paulo", "SP", "01234-567");

        address.Street.Should().Be("Rua das Flores");
        address.Number.Should().Be("100");
        address.City.Should().Be("São Paulo");
        address.ZipCode.Should().Be("01234567");
    }

    [Fact]
    public void Create_MissingStreet_ThrowsArgumentException()
    {
        var act = () => Address.Create(
            "", "100", "Bairro", "Cidade", "SP", "01234567");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = Address.Create("Rua A", "1", "Bairro", "SP", "SP", "01001000");
        var b = Address.Create("Rua A", "1", "Bairro", "SP", "SP", "01001000");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = Address.Create("Rua A", "1", "Bairro", "SP", "SP", "01001000");
        var b = Address.Create("Rua B", "2", "Bairro", "SP", "SP", "01001000");

        a.Should().NotBe(b);
    }
}
