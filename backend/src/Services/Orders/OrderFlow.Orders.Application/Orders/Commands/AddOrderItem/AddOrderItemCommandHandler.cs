using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;

public sealed class AddOrderItemCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AddOrderItemCommand, Result>
{
    public async Task<Result> Handle(AddOrderItemCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(request.OrderId));

        try
        {
            var unitPrice = Money.FromDecimal(request.UnitPrice);
            order.AddItem(request.ProductId, request.ProductName, unitPrice, request.Quantity);

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OrderDomainException ex)
        {
            return Result.Failure(new Error(ex.Code, ex.Message));
        }
    }
}
