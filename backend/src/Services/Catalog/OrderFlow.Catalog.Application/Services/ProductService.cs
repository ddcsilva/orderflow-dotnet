using FluentValidation;
using Microsoft.Extensions.Logging;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.Catalog.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Application.Services;

public sealed class ProductService(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator,
    ILogger<ProductService> logger) : IProductService
{
    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct);
        return product is null ? null : MapToDto(product);
    }

    public async Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var product = await productRepository.GetBySkuAsync(sku, ct);
        return product is null ? null : MapToDto(product);
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductSearchRequest request, CancellationToken ct = default)
    {
        var (items, totalCount) = await productRepository.SearchAsync(
            request.SearchTerm,
            request.CategoryId,
            request.MinPrice,
            request.MaxPrice,
            request.Page,
            request.PageSize,
            ct);

        return new PagedResult<ProductDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, ct)
            ?? throw new KeyNotFoundException($"Category with ID '{request.CategoryId}' not found.");

        if (await productRepository.SkuExistsAsync(request.Sku, ct))
            throw new InvalidOperationException($"A product with SKU '{request.Sku}' already exists.");

        var product = Product.Create(
            request.Name,
            request.Sku,
            request.Price,
            request.StockQuantity,
            request.CategoryId,
            request.Description);

        await productRepository.AddAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Product created: {ProductId} - {ProductName}", product.Id, product.Name);

        return MapToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var product = await productRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Product with ID '{id}' not found.");

        product.Update(request.Name, request.Description, request.Price, request.StockQuantity);

        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Product updated: {ProductId}", product.Id);

        return MapToDto(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Product with ID '{id}' not found.");

        product.Deactivate();
        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Product deactivated: {ProductId}", product.Id);
    }

    private static ProductDto MapToDto(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Sku,
        product.Price,
        product.StockQuantity,
        product.IsActive,
        product.CategoryId,
        product.Category?.Name,
        product.CreatedAt,
        product.UpdatedAt);
}