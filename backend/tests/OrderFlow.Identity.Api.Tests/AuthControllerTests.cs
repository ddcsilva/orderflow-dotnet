using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Identity.Api.Data;
using OrderFlow.Identity.Api.DTOs;

namespace OrderFlow.Identity.Api.Tests;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task Register_ValidRequest_ReturnsAuthResponse()
    {
        var client = CreateClient();
        var request = new RegisterRequest("John Doe", "john@test.com", "Test@1234", "Test@1234");

        var response = await client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.User.Email.Should().Be("john@test.com");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var client = CreateClient();
        var request = new RegisterRequest("Jane", "jane@test.com", "Test@1234", "Test@1234");
        await client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        var client = CreateClient();
        var registerRequest = new RegisterRequest("User", "login@test.com", "Test@1234", "Test@1234");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);

        var loginRequest = new LoginRequest("login@test.com", "Test@1234");
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        auth!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var client = CreateClient();
        var registerRequest = new RegisterRequest("User", "wrong@test.com", "Test@1234", "Test@1234");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);

        var loginRequest = new LoginRequest("wrong@test.com", "WrongPassword1!");
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokens()
    {
        var client = CreateClient();
        var registerRequest = new RegisterRequest("User", "refresh@test.com", "Test@1234", "Test@1234");
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        var refreshRequest = new RefreshTokenRequest(auth!.AccessToken, auth.RefreshToken);
        var response = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        newAuth!.AccessToken.Should().NotBeNullOrEmpty();
        newAuth.RefreshToken.Should().NotBe(auth.RefreshToken);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUserInfo()
    {
        var client = CreateClient();
        var registerRequest = new RegisterRequest("User", "me@test.com", "Test@1234", "Test@1234");
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest, TestContext.Current.CancellationToken);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
