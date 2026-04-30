using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;

public sealed record AddOrderItemCommand(
    Guid OrderId,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity) : IRequest<Result>;
