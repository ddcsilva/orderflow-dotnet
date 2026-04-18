using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Domain.Interfaces;

public interface ICategoryRepository : IRepository<Category>
{
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default);
}