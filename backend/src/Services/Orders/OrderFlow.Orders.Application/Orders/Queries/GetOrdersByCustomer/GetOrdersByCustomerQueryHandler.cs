using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;
using OrderFlow.Orders.Application.Common.Interfaces;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

public sealed class GetOrdersByCustomerQueryHandler(IOrderReadRepository readRepository)
    : IRequestHandler<GetOrdersByCustomerQuery, Result<IReadOnlyList<OrderSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<OrderSummaryDto>>> Handle(
        GetOrdersByCustomerQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var orders = await readRepository.GetOrdersByCustomerAsync(
            request.CustomerId, request.Page, request.PageSize, ct);

        return Result<IReadOnlyList<OrderSummaryDto>>.Success(orders);
    }
}
