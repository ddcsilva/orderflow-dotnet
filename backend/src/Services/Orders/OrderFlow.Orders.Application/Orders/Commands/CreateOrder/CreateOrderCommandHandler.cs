using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var address = Address.Create(
            request.Street, request.Number, request.Neighborhood,
            request.City, request.State, request.ZipCode,
            request.Complement);

        var order = Order.Create(request.CustomerId, address);

        await orderRepository.AddAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Guid>.Success(order.Id);
    }
}
