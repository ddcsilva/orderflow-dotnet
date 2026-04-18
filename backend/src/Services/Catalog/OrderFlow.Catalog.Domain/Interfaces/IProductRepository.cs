using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        Guid? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default);
}