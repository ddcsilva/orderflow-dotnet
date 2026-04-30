namespace OrderFlow.Orders.Application.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided.");
}

public static class OrderErrors
{
    public static Error NotFound(Guid orderId) =>
        new("Order.NotFound", $"Order with ID '{orderId}' was not found.");

    public static Error AlreadyConfirmed { get; } =
        new("Order.AlreadyConfirmed", "The order has already been confirmed.");

    public static Error EmptyOrder { get; } =
        new("Order.Empty", "Cannot confirm an order with no items.");

    public static Error InvalidStatusTransition(string from, string to) =>
        new("Order.InvalidTransition", $"Cannot transition from '{from}' to '{to}'.");
}
