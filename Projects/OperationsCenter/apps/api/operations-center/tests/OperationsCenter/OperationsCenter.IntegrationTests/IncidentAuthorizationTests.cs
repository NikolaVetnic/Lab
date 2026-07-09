using System.Net;
using System.Net.Http.Json;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class IncidentAuthorizationTests(IntegrationTestWebApplicationFactory factory)
{
    [Fact]
    public async Task ListIncidents_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/incidents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetIncidentAudit_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/incidents/{Guid.NewGuid()}/audit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OperationsHubNegotiate_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/operations/negotiate?negotiateVersion=1")
        {
            Content = new StringContent(string.Empty)
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateIncident_WhenUserIsViewer_ReturnsForbidden()
    {
        using var client = await IntegrationTestAuthHelper.CreateAuthenticatedClientAsync(
            factory,
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
            factory,
            email: $"viewer-{Guid.NewGuid()}@operations-center.local",
            password: "Viewer123!",
            role: SystemRole.Viewer);

        var response = await client.GetAsync("/incidents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
