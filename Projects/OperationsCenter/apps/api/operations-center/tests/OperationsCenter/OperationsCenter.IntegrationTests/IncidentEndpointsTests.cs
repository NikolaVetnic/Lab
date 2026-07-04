using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OperationsCenter.Infrastructure.Persistence;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.IntegrationTests;

public sealed class IncidentEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task CreateIncident_WhenRequestIsValid_ReturnsCreatedWithExpectedBody()
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            title = $"API create test {Guid.NewGuid()}",
            description = "Incident created from integration test.",
            severity = IncidentSeverity.High
        };

        var response = await client.PostAsJsonAsync("/incidents", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(request.title, body.Title);
        Assert.Equal(request.description, body.Description);
        Assert.Equal(IncidentSeverity.High, body.Severity);
        Assert.Equal(IncidentStatus.Open, body.Status);
        Assert.Equal(TimeSpan.Zero, body.CreatedAt.Offset);
    }

    [Fact]
    public async Task CreateIncident_WhenRequestIsValid_WritesCreatedAuditEvent()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/incidents",
            new
            {
                title = $"Audit create {Guid.NewGuid()}",
                description = "Audit create integration",
                severity = IncidentSeverity.Low
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(body);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OperationsCenterDbContext>();

        AuditEvent? auditEvent = await dbContext.AuditEvents
            .AsNoTracking()
            .OrderByDescending(audit => audit.OccurredAt)
            .FirstOrDefaultAsync(audit =>
                audit.EntityType == "Incident" &&
                audit.EntityId == body.Id &&
                audit.Action == "Created");

        Assert.NotNull(auditEvent);
        Assert.Null(auditEvent.MetadataJson);
    }

    [Fact]
    public async Task CreateIncident_WhenTitleIsMissingOrInvalid_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var missingTitleResponse = await client.PostAsJsonAsync(
            "/incidents",
            new
            {
                description = "Missing title",
                severity = IncidentSeverity.Low
            });

        Assert.Equal(HttpStatusCode.BadRequest, missingTitleResponse.StatusCode);

        var invalidTitleResponse = await client.PostAsJsonAsync(
            "/incidents",
            new
            {
                title = "   ",
                description = "Invalid title",
                severity = IncidentSeverity.Low
            });

        Assert.Equal(HttpStatusCode.BadRequest, invalidTitleResponse.StatusCode);
    }

    [Fact]
    public async Task ListIncidents_WhenCalled_ReturnsNewestIncidentsFirst()
    {
        using var client = _factory.CreateClient();

        var older = await CreateIncidentAsync(client, $"Older incident {Guid.NewGuid()}");
        await Task.Delay(50);
        var newer = await CreateIncidentAsync(client, $"Newer incident {Guid.NewGuid()}");

        var response = await client.GetAsync("/incidents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var incidents = await response.Content.ReadFromJsonAsync<List<IncidentDto>>();
        Assert.NotNull(incidents);

        var olderIndex = incidents.FindIndex(incident => incident.Id == older.Id);
        var newerIndex = incidents.FindIndex(incident => incident.Id == newer.Id);

        Assert.NotEqual(-1, olderIndex);
        Assert.NotEqual(-1, newerIndex);
        Assert.True(newerIndex < olderIndex);
    }

    [Fact]
    public async Task GetIncidentById_WhenIncidentExists_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var createdIncident = await CreateIncidentAsync(client, $"Get incident {Guid.NewGuid()}");

        var response = await client.GetAsync($"/incidents/{createdIncident.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var incident = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(incident);
        Assert.Equal(createdIncident.Id, incident.Id);
    }

    [Fact]
    public async Task GetIncidentById_WhenIncidentDoesNotExist_ReturnsNotFoundProblemDetails()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Incident not found.", title.GetString());
    }

    [Fact]
    public async Task UpdateIncidentStatus_WhenTransitionIsValid_ReturnsOkWithUpdatedIncident()
    {
        using var client = _factory.CreateClient();

        var createdIncident = await CreateIncidentAsync(client, $"Update status {Guid.NewGuid()}");

        var response = await client.PatchAsJsonAsync(
            $"/incidents/{createdIncident.Id}/status",
            new { status = IncidentStatus.InProgress });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(updated);
        Assert.Equal(createdIncident.Id, updated.Id);
        Assert.Equal(IncidentStatus.InProgress, updated.Status);
    }

    [Fact]
    public async Task UpdateIncidentStatus_WhenTransitionIsValid_WritesStatusChangedAuditEvent()
    {
        using var client = _factory.CreateClient();

        var createdIncident = await CreateIncidentAsync(client, $"Audit status {Guid.NewGuid()}");

        var response = await client.PatchAsJsonAsync(
            $"/incidents/{createdIncident.Id}/status",
            new { status = IncidentStatus.InProgress });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OperationsCenterDbContext>();

        AuditEvent? auditEvent = await dbContext.AuditEvents
            .AsNoTracking()
            .OrderByDescending(audit => audit.OccurredAt)
            .FirstOrDefaultAsync(audit =>
                audit.EntityType == "Incident" &&
                audit.EntityId == createdIncident.Id &&
                audit.Action == "StatusChanged");

        Assert.NotNull(auditEvent);
        Assert.NotNull(auditEvent.MetadataJson);

        using JsonDocument metadata = JsonDocument.Parse(auditEvent.MetadataJson);
        Assert.Equal("Open", metadata.RootElement.GetProperty("oldStatus").GetString());
        Assert.Equal("InProgress", metadata.RootElement.GetProperty("newStatus").GetString());
    }

    [Fact]
    public async Task UpdateIncidentStatus_WhenIncidentDoesNotExist_ReturnsNotFoundProblemDetails()
    {
        using var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/incidents/{Guid.NewGuid()}/status",
            new { status = IncidentStatus.InProgress });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Incident not found.", title.GetString());
    }

    [Fact]
    public async Task UpdateIncidentStatus_WhenTransitionIsInvalid_ReturnsConflictProblemDetails()
    {
        using var client = _factory.CreateClient();

        var createdIncident = await CreateIncidentAsync(client, $"Invalid transition {Guid.NewGuid()}");

        var response = await client.PatchAsJsonAsync(
            $"/incidents/{createdIncident.Id}/status",
            new { status = IncidentStatus.Closed });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("title", out var title));
        Assert.Equal("Invalid incident status transition.", title.GetString());
    }

    [Fact]
    public async Task GetIncidentAudits_WhenIncidentHasChanges_ReturnsCreatedAndStatusChangedEvents()
    {
        using var client = _factory.CreateClient();

        var createdIncident = await CreateIncidentAsync(client, $"Incident audits {Guid.NewGuid()}");

        var updateResponse = await client.PatchAsJsonAsync(
            $"/incidents/{createdIncident.Id}/status",
            new { status = IncidentStatus.InProgress });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var response = await client.GetAsync($"/incidents/{createdIncident.Id}/audits");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audits = await response.Content.ReadFromJsonAsync<List<AuditEventDto>>();
        Assert.NotNull(audits);
        Assert.True(audits.Count >= 2);
        Assert.Contains(audits, audit => audit.Action == "Created");
        Assert.Contains(audits, audit => audit.Action == "StatusChanged");
        Assert.All(audits, audit => Assert.Equal(createdIncident.Id, audit.EntityId));
    }

    [Fact]
    public async Task GetAudits_WhenActionFilterIsProvided_ReturnsOnlyMatchingAction()
    {
        using var client = _factory.CreateClient();

        var createdIncident = await CreateIncidentAsync(client, $"Filtered audits {Guid.NewGuid()}");

        var updateResponse = await client.PatchAsJsonAsync(
            $"/incidents/{createdIncident.Id}/status",
            new { status = IncidentStatus.InProgress });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var response = await client.GetAsync(
            $"/audits?entityType=Incident&entityId={createdIncident.Id}&action=StatusChanged");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audits = await response.Content.ReadFromJsonAsync<List<AuditEventDto>>();
        Assert.NotNull(audits);
        Assert.NotEmpty(audits);
        Assert.All(audits, audit => Assert.Equal("StatusChanged", audit.Action));
        Assert.All(audits, audit => Assert.Equal(createdIncident.Id, audit.EntityId));
    }

    private static async Task<IncidentDto> CreateIncidentAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/incidents",
            new
            {
                title,
                description = "Generated by integration test",
                severity = IncidentSeverity.Medium
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<IncidentDto>();
        Assert.NotNull(dto);
        return dto;
    }

    private sealed record IncidentDto(
        Guid Id,
        string Title,
        string? Description,
        IncidentSeverity Severity,
        IncidentStatus Status,
        DateTimeOffset CreatedAt);

    private sealed record AuditEventDto(
        Guid Id,
        string EntityType,
        Guid EntityId,
        string Action,
        DateTimeOffset OccurredAt,
        string? ActorId,
        string? MetadataJson);
}
