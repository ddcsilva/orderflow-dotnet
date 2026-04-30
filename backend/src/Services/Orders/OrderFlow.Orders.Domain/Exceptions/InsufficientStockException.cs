namespace OrderFlow.Orders.Domain.Exceptions;

public sealed class InsufficientStockException : Exception
{
    public static string Code => "INSUFFICIENT_STOCK";
    public Guid ProductId { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(Guid productId, int requested, int available)
        : base($"Insufficient stock for product '{productId}'. Requested: {requested}, Available: {available}.")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }
}
