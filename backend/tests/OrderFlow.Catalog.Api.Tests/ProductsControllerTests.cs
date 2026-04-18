using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Api.Tests;

public class ProductsControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<CategoryDto> CreateCategoryAsync()
    {
        var request = new CreateCategoryRequest($"Test Category {Guid.NewGuid():N}"[..30], "Category for testing");
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    [Fact]
    public async Task Create_ValidProduct_ReturnsCreated()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var request = new CreateProductRequest(
            "Laptop Pro",
            "High-performance laptop",
            "LAPTOP-001",
            2999.99m,
            10,
            category.Id);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product.Should().NotBeNull();
        product!.Name.Should().Be("Laptop Pro");
        product.Sku.Should().Be("LAPTOP-001");
        product.Price.Should().Be(2999.99m);
    }

    [Fact]
    public async Task Search_WithTerm_ReturnsFilteredResults()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Wireless Mouse", null, $"MOUSE-{Guid.NewGuid():N}"[..20], 49.99m, 100, category.Id));
        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Wireless Keyboard", null, $"KB-{Guid.NewGuid():N}"[..20], 79.99m, 50, category.Id));
        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Monitor 27\"", null, $"MON-{Guid.NewGuid():N}"[..20], 399.99m, 20, category.Id));

        // Act
        var response = await _client.GetAsync("/api/v1/products?searchTerm=Wireless");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Items.Should().OnlyContain(p => p.Name.Contains("Wireless"));
    }

    [Fact]
    public async Task Create_DuplicateSku_ReturnsConflict()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var sku = $"DUP-{Guid.NewGuid():N}"[..15];

        await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Product 1", null, sku, 10m, 1, category.Id));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Product 2", null, sku, 20m, 2, category.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_ExistingProduct_ReturnsNoContent()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "To Delete", null, $"DEL-{Guid.NewGuid():N}"[..15], 10m, 1, category.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<ProductDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/products/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft-deleted (filtered by query filter)
        var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
