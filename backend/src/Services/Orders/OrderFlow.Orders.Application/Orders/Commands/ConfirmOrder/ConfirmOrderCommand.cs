using MediatR;
using OrderFlow.Orders.Application.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.ConfirmOrder;

public sealed record ConfirmOrderCommand(Guid OrderId) : IRequest<Result>;
