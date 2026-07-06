using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.IntegrationTests;

public sealed class IncidentAuthorizationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task ListIncidents_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/incidents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateIncident_WhenUserIsViewer_ReturnsForbidden()
    {
        using var client = await IntegrationTestAuthHelper.CreateAuthenticatedClientAsync(
            _factory,
            email: $"viewer-{Guid.NewGuid()}@operations-center.local",
            password: "Viewer123!",
            role: SystemRole.Viewer);

        var response = await client.PostAsJsonAsync(
            "/incidents",
            new
            {
                title = $"Viewer create {Guid.NewGuid()}",
                description = "Viewer should not be able to create incidents",
                severity = IncidentSeverity.Low
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListIncidents_WhenUserIsViewer_ReturnsOk()
    {
        using var client = await IntegrationTestAuthHelper.CreateAuthenticatedClientAsync(
            _factory,
            email: $"viewer-{Guid.NewGuid()}@operations-center.local",
            password: "Viewer123!",
            role: SystemRole.Viewer);

        var response = await client.GetAsync("/incidents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
