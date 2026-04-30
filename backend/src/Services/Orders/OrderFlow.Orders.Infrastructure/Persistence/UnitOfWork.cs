using MediatR;
using OrderFlow.Orders.Infrastructure.Extensions;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Infrastructure.Persistence;

public sealed class UnitOfWork(OrdersDbContext dbContext, IMediator mediator) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        await mediator.DispatchDomainEventsAsync(dbContext, ct);
        return await dbContext.SaveChangesAsync(ct);
    }
}
