using OrderFlow.Orders.Application.Orders.Queries.GetOrderById;
using OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

namespace OrderFlow.Orders.Application.Common.Interfaces;

public interface IOrderReadRepository
{
    Task<OrderDetailDto?> GetOrderDetailAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummaryDto>> GetOrdersByCustomerAsync(
        Guid customerId, int page, int pageSize, CancellationToken ct = default);
}
