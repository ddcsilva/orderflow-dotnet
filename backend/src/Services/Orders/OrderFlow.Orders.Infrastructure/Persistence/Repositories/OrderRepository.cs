using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Interfaces;

namespace OrderFlow.Orders.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(OrdersDbContext dbContext) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Order entity, CancellationToken ct = default)
    {
        await dbContext.Orders.AddAsync(entity, ct);
    }

    public void Update(Order entity)
    {
        dbContext.Orders.Update(entity);
    }

    public void Remove(Order entity)
    {
        dbContext.Orders.Remove(entity);
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber.Value == orderNumber, ct);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        return await dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
    }
}
