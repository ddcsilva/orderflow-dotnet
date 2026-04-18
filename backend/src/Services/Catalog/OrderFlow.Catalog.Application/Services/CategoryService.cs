using FluentValidation;
using Microsoft.Extensions.Logging;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Domain.Entities;
using OrderFlow.Catalog.Domain.Interfaces;
using OrderFlow.SharedKernel;

namespace OrderFlow.Catalog.Application.Services;

public sealed partial class CategoryService(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator,
    ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, ct);
        return category is null ? null : MapToDto(category);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var categories = await categoryRepository.GetActiveAsync(ct);
        return categories.Select(MapToDto).ToList();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        if (await categoryRepository.NameExistsAsync(request.Name, ct))
            throw new InvalidOperationException($"Já existe uma categoria com o nome '{request.Name}'.");

        var category = Category.Create(request.Name, request.Description);

        await categoryRepository.AddAsync(category, ct);
        await unitOfWork.SaveChangesAsync(ct);

        LogCategoryCreated(logger, category.Id, category.Name);

        return MapToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        var category = await categoryRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Categoria com ID '{id}' não foi encontrada.");

        category.Update(request.Name, request.Description);

        categoryRepository.Update(category);
        await unitOfWork.SaveChangesAsync(ct);

        LogCategoryUpdated(logger, category.Id);

        return MapToDto(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Categoria com ID '{id}' não foi encontrada.");

        category.Deactivate();
        categoryRepository.Update(category);
        await unitOfWork.SaveChangesAsync(ct);

        LogCategoryDeactivated(logger, category.Id);
    }

    private static CategoryDto MapToDto(Category category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.IsActive,
        category.Products.Count,
        category.CreatedAt,
        category.UpdatedAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Category created: {CategoryId} - {CategoryName}")]
    private static partial void LogCategoryCreated(ILogger logger, Guid categoryId, string categoryName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Category updated: {CategoryId}")]
    private static partial void LogCategoryUpdated(ILogger logger, Guid categoryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Category deactivated: {CategoryId}")]
    private static partial void LogCategoryDeactivated(ILogger logger, Guid categoryId);
}
