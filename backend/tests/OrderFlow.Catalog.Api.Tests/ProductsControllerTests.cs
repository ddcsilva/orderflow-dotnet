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
        var request = new CreateCategoryRequest(
            $"Categoria Teste {Guid.NewGuid():N}"[..30],
            "Categoria para testes");
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    private async Task<ProductDto> CreateProductAsync(Guid categoryId, string? name = null, string? sku = null)
    {
        var request = new CreateProductRequest(
            name ?? "Produto Teste",
            "Descrição de teste",
            sku ?? $"SKU-{Guid.NewGuid():N}"[..20],
            99.99m,
            10,
            categoryId);
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    [Fact]
    public async Task Create_ValidProduct_ReturnsCreated()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var sku = $"LAPTOP-{Guid.NewGuid():N}"[..20];
        var request = new CreateProductRequest(
            "Laptop Pro",
            "Laptop de alta performance",
            sku,
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
        product.Sku.Should().Be(sku.ToUpperInvariant());
        product.Price.Should().Be(2999.99m);
        product.IsActive.Should().BeTrue();
        product.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public async Task GetById_ExistingProduct_ReturnsOk()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);

        // Act
        var response = await _client.GetAsync($"/api/v1/products/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product!.Id.Should().Be(created.Id);
        product.Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task GetById_NonExistentProduct_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_WithTerm_ReturnsFilteredResults()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        await CreateProductAsync(category.Id, "Wireless Mouse", $"MOUSE-{Guid.NewGuid():N}"[..20]);
        await CreateProductAsync(category.Id, "Wireless Keyboard", $"KB-{Guid.NewGuid():N}"[..20]);
        await CreateProductAsync(category.Id, "Monitor 27 polegadas", $"MON-{Guid.NewGuid():N}"[..20]);

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
    public async Task Update_ExistingProduct_ReturnsOk()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);
        var updateRequest = new UpdateProductRequest(
            "Nome Atualizado",
            "Descrição atualizada",
            199.99m,
            50);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/products/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProductDto>();
        updated!.Name.Should().Be("Nome Atualizado");
        updated.Price.Should().Be(199.99m);
        updated.StockQuantity.Should().Be(50);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_DuplicateSku_ReturnsConflict()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var sku = $"DUP-{Guid.NewGuid():N}"[..15];
        await CreateProductAsync(category.Id, sku: sku);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            "Produto 2", null, sku, 20m, 2, category.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var request = new CreateProductRequest(
            "",
            null,
            $"SKU-{Guid.NewGuid():N}"[..15],
            10m,
            1,
            category.Id);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InvalidCategoryId_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateProductRequest(
            "Produto Teste",
            null,
            $"SKU-{Guid.NewGuid():N}"[..15],
            10m,
            1,
            Guid.NewGuid());

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingProduct_ReturnsNoContent()
    {
        // Arrange
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/products/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verifica soft-delete (filtrado pelo query filter)
        var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
