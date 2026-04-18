namespace OrderFlow.Catalog.Application.DTOs;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    Guid CategoryId,
    string? CategoryName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateProductRequest(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    Guid CategoryId);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity);

public sealed record ProductSearchRequest(
    string? SearchTerm = null,
    Guid? CategoryId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int Page = 1,
    int PageSize = 20);