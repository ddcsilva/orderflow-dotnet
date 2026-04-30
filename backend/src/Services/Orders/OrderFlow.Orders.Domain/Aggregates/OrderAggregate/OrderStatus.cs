using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

public sealed class OrderStatus : ValueObject
{
    public static readonly OrderStatus Pending = new("Pending");
    public static readonly OrderStatus Confirmed = new("Confirmed");
    public static readonly OrderStatus Shipped = new("Shipped");
    public static readonly OrderStatus Delivered = new("Delivered");
    public static readonly OrderStatus Cancelled = new("Cancelled");

    public string Value { get; }

    private OrderStatus(string value)
    {
        Value = value;
    }

    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        [Pending.Value] = [Confirmed.Value, Cancelled.Value],
        [Confirmed.Value] = [Shipped.Value, Cancelled.Value],
        [Shipped.Value] = [Delivered.Value],
        [Delivered.Value] = [],
        [Cancelled.Value] = []
    };

    public bool CanTransitionTo(OrderStatus newStatus)
    {
        return ValidTransitions.TryGetValue(Value, out var validTargets)
               && validTargets.Contains(newStatus.Value);
    }

    public OrderStatus TransitionTo(OrderStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new OrderDomainException(
                $"Invalid status transition from '{Value}' to '{newStatus.Value}'.",
                "INVALID_STATUS_TRANSITION");
        }

        return newStatus;
    }

    public bool IsFinal => this == Delivered || this == Cancelled;

    public static OrderStatus FromString(string status)
    {
        return status switch
        {
            "Pending" => Pending,
            "Confirmed" => Confirmed,
            "Shipped" => Shipped,
            "Delivered" => Delivered,
            "Cancelled" => Cancelled,
            _ => throw new OrderDomainException($"Unknown order status: '{status}'.", "UNKNOWN_STATUS")
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
