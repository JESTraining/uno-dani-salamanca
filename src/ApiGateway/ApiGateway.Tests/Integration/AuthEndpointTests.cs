using System.Net;
using System.Net.Http.Json;
using ApiGateway.API.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiGateway.Tests.Integration;

public class AuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsTokenThroughTheFullPipeline()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin123!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("Admin", body.Role);
        Assert.Equal("Bearer", body.TokenType);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsHealthyWithNoRegisteredChecks()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
