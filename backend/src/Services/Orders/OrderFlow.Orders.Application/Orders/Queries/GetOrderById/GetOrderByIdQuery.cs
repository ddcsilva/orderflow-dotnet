using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDetailDto>>;
