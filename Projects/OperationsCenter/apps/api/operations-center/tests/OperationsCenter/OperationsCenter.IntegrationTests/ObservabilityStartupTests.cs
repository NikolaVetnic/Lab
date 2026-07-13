using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace OperationsCenter.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ObservabilityStartupTests(IntegrationTestWebApplicationFactory factory)
{
    private readonly IntegrationTestWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Api_WhenTelemetryDisabled_StartsAndServesHealth()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_WhenTelemetryEnabledWithUnreachableCollector_StartsAndServesHealth()
    {
        using var enabledFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenTelemetry:Enabled"] = "true",
                    ["OpenTelemetry:ServiceName"] = "operations-center-api",
                    ["OpenTelemetry:OtlpEndpoint"] = "http://127.0.0.1:65500"
                })));

        using var client = enabledFactory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
