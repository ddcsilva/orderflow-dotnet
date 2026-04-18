namespace OrderFlow.Catalog.Application.DTOs;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int ProductCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateCategoryRequest(
    string Name,
    string? Description);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description);