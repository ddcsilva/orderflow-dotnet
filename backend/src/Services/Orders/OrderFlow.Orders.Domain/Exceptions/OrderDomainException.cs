namespace OrderFlow.Orders.Domain.Exceptions;

public sealed class OrderDomainException : Exception
{
    public string Code { get; }

    public OrderDomainException(string message, string code = "ORDER_DOMAIN_ERROR")
        : base(message)
    {
        Code = code;
    }

    public OrderDomainException(string message, string code, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
