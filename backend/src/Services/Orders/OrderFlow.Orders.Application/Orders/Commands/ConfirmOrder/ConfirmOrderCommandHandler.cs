using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.ConfirmOrder;

public sealed class ConfirmOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConfirmOrderCommand, Result>
{
    public async Task<Result> Handle(ConfirmOrderCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(request.OrderId));

        try
        {
            order.Confirm();
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OrderDomainException ex)
        {
            return Result.Failure(new Error(ex.Code, ex.Message));
        }
    }
}
