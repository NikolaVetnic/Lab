using System.Net;
using System.Net.Http.Json;

namespace OperationsCenter.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class HealthEndpointTests(IntegrationTestWebApplicationFactory factory)
{
    [Fact]
    public async Task GetHealth_WhenCalled_ReturnsOk()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthDto>();
        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
    }

    [Fact]
    public async Task GetReadiness_WhenCalled_ReturnsOk()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ReadinessDto>();
        Assert.NotNull(payload);
        Assert.Equal("Ready", payload.Status);
    }

    private sealed record HealthDto(string Status);

    private sealed record ReadinessDto(string Status);
}
