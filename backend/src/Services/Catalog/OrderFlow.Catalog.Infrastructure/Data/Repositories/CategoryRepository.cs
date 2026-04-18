using Microsoft.EntityFrameworkCore;
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.Catalog.Domain.Interfaces;

namespace OrderFlow.Catalog.Infrastructure.Data.Repositories;

public sealed class CategoryRepository(CatalogDbContext context) : ICategoryRepository
{
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Categories
            .Include(c => c.Products)
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Category entity, CancellationToken ct = default)
    {
        await context.Categories.AddAsync(entity, ct);
    }

    public void Update(Category entity)
    {
        context.Categories.Update(entity);
    }

    public void Remove(Category entity)
    {
        context.Categories.Remove(entity);
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken ct = default)
    {
        return await context.Categories
            .AnyAsync(c => c.Name == name, ct);
    }

    public async Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default)
    {
        return await context.Categories
            .Include(c => c.Products)
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }
}
