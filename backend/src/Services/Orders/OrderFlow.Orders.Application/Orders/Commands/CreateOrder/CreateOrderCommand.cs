using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;

namespace OrderFlow.Orders.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    string Street,
    string Number,
    string Neighborhood,
    string City,
    string State,
    string ZipCode,
    string? Complement = null) : IRequest<Result<Guid>>;
