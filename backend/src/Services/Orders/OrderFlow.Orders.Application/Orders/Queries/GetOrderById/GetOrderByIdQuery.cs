using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;
