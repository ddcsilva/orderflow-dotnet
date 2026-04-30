using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OrderFlow.IntegrationTests;

/// <summary>
/// Smoke tests do API Gateway: validam que o pipeline (Auth, YARP routes, health)
/// está corretamente wired sem exigir os serviços downstream rodando.
/// </summary>
public class GatewayRoutingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GatewayRoutingTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Testing"))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedOrdersRoute_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync(new Uri("/api/orders/123", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var response = await _client.GetAsync(new Uri("/totally/unknown/path", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
