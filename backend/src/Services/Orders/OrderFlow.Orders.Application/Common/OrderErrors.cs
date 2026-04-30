using OrderFlow.SharedKernel.Common;

namespace OrderFlow.Orders.Application.Common;

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
