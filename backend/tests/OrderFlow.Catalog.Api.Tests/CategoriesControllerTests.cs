using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Api.Tests;

public class CategoriesControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<CategoryDto> CreateCategoryAsync(string? name = null, string? description = null)
    {
        var request = new CreateCategoryRequest(
            name ?? $"Categoria {Guid.NewGuid():N}"[..30],
            description ?? "Descrição de teste");
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    [Fact]
    public async Task Create_ValidCategory_ReturnsCreated()
    {
        // Arrange
        var name = $"Eletrônicos {Guid.NewGuid():N}"[..30];
        var request = new CreateCategoryRequest(name, "Dispositivos eletrônicos");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Name.Should().Be(name);
        category.Id.Should().NotBeEmpty();
        category.IsActive.Should().BeTrue();
        category.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAll_WithActiveCategories_ReturnsOk()
    {
        // Arrange
        await CreateCategoryAsync();
        await CreateCategoryAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
        categories!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetById_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var created = await CreateCategoryAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/categories/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.Name.Should().Be(created.Name);
    }

    [Fact]
    public async Task GetById_NonExistentCategory_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/categories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var created = await CreateCategoryAsync();
        var updateRequest = new UpdateCategoryRequest("Nome Atualizado", "Descrição atualizada");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/categories/{created.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        updated!.Name.Should().Be("Nome Atualizado");
        updated.Description.Should().Be("Descrição atualizada");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ExistingCategory_ReturnsNoContent()
    {
        // Arrange
        var created = await CreateCategoryAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/categories/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/v1/categories/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var category = await CreateCategoryAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new CreateCategoryRequest(category.Name, null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateCategoryRequest("", null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
