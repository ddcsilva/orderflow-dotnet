using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.ValueObjects;

public sealed class OrderNumber : ValueObject
{
    public string Value { get; }

    private OrderNumber(string value)
    {
        Value = value;
    }

    public static OrderNumber Create()
    {
        // Formato: ORD-YYYYMMDD-XXXXX (ex: ORD-20260415-A3F8B)
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var randomPart = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        return new OrderNumber($"ORD-{datePart}-{randomPart}");
    }

    public static OrderNumber FromExisting(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new OrderNumber(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
