using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.IntegrationTests;

internal static class IntegrationTestAuthHelper
{
    public static Task EnsureUserAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password,
        SystemRole role)
    {
        return EnsureUserInternalAsync(factory, email, password, role);
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password,
        SystemRole role)
    {
        await EnsureUserInternalAsync(factory, email, password, role);

        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Login response did not include access token.");
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);

        return client;
    }

    private static async Task EnsureUserInternalAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password,
        SystemRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OperationsCenterDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await dbContext.Database.MigrateAsync();

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Email == normalizedEmail);
        if (existing is not null)
        {
            return;
        }

        var user = User.Create(normalizedEmail, passwordHasher.Hash(password), role, DateTimeOffset.UtcNow);
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();
    }

    private sealed record LoginResponseDto(string AccessToken, DateTimeOffset ExpiresAtUtc, string TokenType);
}
