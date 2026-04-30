using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<Result>;
