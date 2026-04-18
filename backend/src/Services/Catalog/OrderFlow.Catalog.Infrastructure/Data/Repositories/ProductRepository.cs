using Microsoft.EntityFrameworkCore;
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.Catalog.Domain.Interfaces;

namespace OrderFlow.Catalog.Infrastructure.Data.Repositories;

public sealed class ProductRepository(CatalogDbContext context) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Product entity, CancellationToken ct = default)
    {
        await context.Products.AddAsync(entity, ct);
    }

    public void Update(Product entity)
    {
        context.Products.Update(entity);
    }

    public void Remove(Product entity)
    {
        context.Products.Remove(entity);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Sku == sku, ct);
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(
        Guid categoryId, CancellationToken ct = default)
    {
        return await context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        Guid? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p =>
                p.Name.Contains(searchTerm) ||
                p.Sku.Contains(searchTerm));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default)
    {
        return await context.Products
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Sku == sku, ct);
    }
}