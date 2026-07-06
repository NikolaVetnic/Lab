using System.Text.Json;
using OperationsCenter.Application.Incidents.Commands.CreateIncident;
using OperationsCenter.Application.Incidents.Commands.UpdateIncidentStatus;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.UnitTests.Application.Incidents.Commands;

public sealed class IncidentAuditCommandHandlerTests
{
    [Fact]
    public async Task CreateIncidentCommandHandler_WhenIncidentIsCreated_WritesCreatedAuditEvent()
    {
        var dbContext = new FakeOperationsCenterDbContext();
        var handler = new CreateIncidentCommandHandler(dbContext);
        var actorUserId = Guid.NewGuid();

        var command = new CreateIncidentCommand("Audit create", "Create audit event", IncidentSeverity.Medium, actorUserId);

        var response = await handler.Handle(command, CancellationToken.None);

        AuditEvent auditEvent = Assert.Single(dbContext.AuditEvents);

        Assert.Equal(response.Id, auditEvent.EntityId);
        Assert.Equal("Incident", auditEvent.EntityType);
        Assert.Equal("Created", auditEvent.Action);
        Assert.Equal(actorUserId.ToString("D"), auditEvent.ActorId);
        Assert.Null(auditEvent.MetadataJson);
    }

    [Fact]
    public async Task UpdateIncidentStatusCommandHandler_WhenStatusChanges_WritesStatusChangedAuditEvent()
    {
        var incident = Incident.Create("Audit status", "Status change", IncidentSeverity.High, Guid.NewGuid());
        var dbContext = new FakeOperationsCenterDbContext(incident);
        var handler = new UpdateIncidentStatusCommandHandler(dbContext);
        var actorUserId = Guid.NewGuid();

        var command = new UpdateIncidentStatusCommand(incident.Id, IncidentStatus.InProgress, actorUserId);

        UpdateIncidentStatusResult result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(UpdateIncidentStatusOutcome.Updated, result.Outcome);
        AuditEvent auditEvent = Assert.Single(dbContext.AuditEvents);
        Assert.Equal("Incident", auditEvent.EntityType);
        Assert.Equal(incident.Id, auditEvent.EntityId);
        Assert.Equal("StatusChanged", auditEvent.Action);
        Assert.Equal(actorUserId.ToString("D"), auditEvent.ActorId);
        Assert.NotNull(auditEvent.MetadataJson);

        using JsonDocument metadata = JsonDocument.Parse(auditEvent.MetadataJson);
        Assert.Equal("Open", metadata.RootElement.GetProperty("oldStatus").GetString());
        Assert.Equal("InProgress", metadata.RootElement.GetProperty("newStatus").GetString());
    }

    private sealed class FakeOperationsCenterDbContext(Incident? existingIncident = null) : IOperationsCenterDbContext
    {
        private readonly Incident? _existingIncident = existingIncident;

        public List<AuditEvent> AuditEvents { get; } = [];

        public Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            AuditEvents.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_existingIncident?.Id == incidentId ? _existingIncident : null);
        }

        public Task<Incident?> GetIncidentByIdForUpdateAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_existingIncident?.Id == incidentId ? _existingIncident : null);
        }

        public Task<bool> IncidentExistsAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_existingIncident is not null && _existingIncident.Id == incidentId);
        }

        public Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Incident>>([]);
        }

        public Task<IReadOnlyList<AuditEvent>> ListIncidentAuditEventsAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>([]);
        }

        public Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
            string? entityType,
            Guid? entityId,
            string? action,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>([]);
        }

        public Task AddUserAsync(User user, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult<User?>(null);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }
    }
}
