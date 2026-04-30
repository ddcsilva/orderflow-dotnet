using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
}
