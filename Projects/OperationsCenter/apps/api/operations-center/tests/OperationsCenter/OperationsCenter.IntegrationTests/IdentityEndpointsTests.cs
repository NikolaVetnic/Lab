using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OperationsCenter.Domain.Identity;

namespace OperationsCenter.IntegrationTests;

public sealed class IdentityEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsBearerToken()
    {
        var email = $"admin-{Guid.NewGuid()}@operations-center.local";
        const string password = "Admin123!";

        await IntegrationTestAuthHelper.EnsureUserAsync(
            _factory,
            email: email,
            password: password,
            role: SystemRole.Admin);

        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email,
                password
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Equal("Bearer", payload.TokenType);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = "unknown@operations-center.local",
                password = "WrongPassword123!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record LoginResponseDto(string AccessToken, DateTimeOffset ExpiresAtUtc, string TokenType);
}
