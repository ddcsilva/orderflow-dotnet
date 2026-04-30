using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Domain.Exceptions;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(OrderErrors.NotFound(request.OrderId));

        try
        {
            order.Cancel(request.Reason);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OrderDomainException ex)
        {
            return Result.Failure(new Error(ex.Code, ex.Message));
        }
    }
}
