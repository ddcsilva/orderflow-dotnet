using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;
using OrderFlow.Orders.Application.Common.Interfaces;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler(IOrderReadRepository readRepository)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderDetailDto>>
{
    public async Task<Result<OrderDetailDto>> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await readRepository.GetOrderDetailAsync(request.OrderId, ct);

        return order is null
            ? Result<OrderDetailDto>.Failure(OrderErrors.NotFound(request.OrderId))
            : Result<OrderDetailDto>.Success(order);
    }
}
