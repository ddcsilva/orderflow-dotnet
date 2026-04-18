using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Domain.Entities;

public sealed class Product : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }

    private Product() { } // EF Core

    public static Product Create(
        string name,
        string sku,
        decimal price,
        int stockQuantity,
        Guid categoryId,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        if (price < 0) 
            throw new ArgumentException("O preço não pode ser negativo.", nameof(price));

        if (stockQuantity < 0) 
            throw new ArgumentException("A quantidade em estoque não pode ser negativa.", nameof(stockQuantity));

        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            Price = price,
            StockQuantity = stockQuantity,
            CategoryId = categoryId,
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, decimal price, int stockQuantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (price < 0) 
            throw new ArgumentException("O preço não pode ser negativo.", nameof(price));
        if (stockQuantity < 0) 
            throw new ArgumentException("A quantidade em estoque não pode ser negativa.", nameof(stockQuantity));

        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        StockQuantity = stockQuantity;
        SetUpdated();
    }

    public void ChangeCategory(Guid newCategoryId)
    {
        if (newCategoryId == Guid.Empty)
            throw new ArgumentException("O ID da categoria não pode ser vazio.", nameof(newCategoryId));

        CategoryId = newCategoryId;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }

    public bool HasSufficientStock(int quantity) => StockQuantity >= quantity;

    public void DecreaseStock(int quantity)
    {
        if (!HasSufficientStock(quantity))
            throw new InvalidOperationException(
                $"Estoque insuficiente. Disponível: {StockQuantity}, Solicitado: {quantity}");

        StockQuantity -= quantity;
        SetUpdated();
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade deve ser positiva.", nameof(quantity));

        StockQuantity += quantity;
        SetUpdated();
    }
}