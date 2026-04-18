using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Api.Tests;

public class CategoriesControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ValidCategory_ReturnsCreated()
    {
        // Arrange
        var request = new CreateCategoryRequest("Electronics", "Electronic devices and gadgets");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Name.Should().Be("Electronics");
        category.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingCategory_ReturnsOk()
    {
        // Arrange
        var createRequest = new CreateCategoryRequest("Books", "All kinds of books");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/categories", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoryDto>();

        // Act
        var response = await _client.GetAsync($"/api/v1/categories/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.Name.Should().Be("Books");
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
