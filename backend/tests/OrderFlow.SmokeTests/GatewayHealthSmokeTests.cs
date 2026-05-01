using System.Net;
using FluentAssertions;

namespace OrderFlow.SmokeTests;

/// <summary>
/// Smoke tests pós-deploy. Apontam para o GATEWAY_URL injetado no pipeline
/// (ex: https://orderflow-gateway-staging.icyforest-xxxx.eastus.azurecontainerapps.io).
/// Skipados localmente quando GATEWAY_URL não está definido.
/// </summary>
public sealed class GatewayHealthSmokeTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string? _baseUrl;

    public GatewayHealthSmokeTests()
    {
        _baseUrl = Environment.GetEnvironmentVariable("GATEWAY_URL");
        _client = new HttpClient
        {
            BaseAddress = string.IsNullOrWhiteSpace(_baseUrl)
                ? new Uri("http://localhost:8080")
                : new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    [Fact]
    public async Task Gateway_HealthLive_ReturnsOk()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(_baseUrl), "GATEWAY_URL not set");

        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Gateway_UnknownRoute_Returns404()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(_baseUrl), "GATEWAY_URL not set");

        var response = await _client.GetAsync(new Uri("/totally/unknown/path", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Gateway_ProtectedOrdersRoute_Returns401WithoutToken()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(_baseUrl), "GATEWAY_URL not set");

        var response = await _client.GetAsync(new Uri("/api/orders/123", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _client.Dispose();
}
