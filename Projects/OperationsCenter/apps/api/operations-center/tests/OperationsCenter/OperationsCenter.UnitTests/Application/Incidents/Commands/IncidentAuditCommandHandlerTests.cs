using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using OperationsCenter.Application.Incidents.Commands.CreateIncident;
using OperationsCenter.Application.Incidents.Realtime;
using OperationsCenter.Application.Incidents.Commands.UpdateIncidentStatus;
using OperationsCenter.Application.Observability;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Contracts.Realtime;
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
        var notifier = new FakeIncidentRealTimeNotifier();
        var handler = new CreateIncidentCommandHandler(dbContext, notifier, CreateTelemetry());
        var actorUserId = Guid.NewGuid();

        var command = new CreateIncidentCommand("Audit create", "Create audit event", IncidentSeverity.Medium, actorUserId);

        var response = await handler.Handle(command, CancellationToken.None);

        AuditEvent auditEvent = Assert.Single(dbContext.AuditEvents);

        Assert.Equal(response.Id, auditEvent.EntityId);
        Assert.Equal("Incident", auditEvent.EntityType);
        Assert.Equal("Created", auditEvent.Action);
        Assert.Equal(actorUserId.ToString("D"), auditEvent.ActorId);
        Assert.Null(auditEvent.MetadataJson);

        IncidentCreatedMessage published = Assert.Single(notifier.CreatedMessages);
        Assert.Equal(response.Id, published.IncidentId);
    }

    [Fact]
    public async Task CreateIncidentCommandHandler_WhenPersistenceFails_DoesNotPublishRealtimeNotification()
    {
        var dbContext = new FakeOperationsCenterDbContext
        {
            ThrowOnSaveChanges = true
        };
        var notifier = new FakeIncidentRealTimeNotifier();
        var handler = new CreateIncidentCommandHandler(dbContext, notifier, CreateTelemetry());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new CreateIncidentCommand("Create fail", "Save should fail", IncidentSeverity.Medium, Guid.NewGuid()),
                CancellationToken.None));

        Assert.Empty(notifier.CreatedMessages);
    }

    [Fact]
    public async Task UpdateIncidentStatusCommandHandler_WhenStatusChanges_WritesStatusChangedAuditEvent()
    {
        var incident = Incident.Create("Audit status", "Status change", IncidentSeverity.High, Guid.NewGuid());
        var dbContext = new FakeOperationsCenterDbContext(incident);
        var notifier = new FakeIncidentRealTimeNotifier();
        var handler = new UpdateIncidentStatusCommandHandler(dbContext, notifier, CreateTelemetry());
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

        IncidentStatusChangedMessage published = Assert.Single(notifier.StatusChangedMessages);
        Assert.Equal(incident.Id, published.IncidentId);
        Assert.Equal("Open", published.PreviousStatus);
        Assert.Equal("InProgress", published.NewStatus);
    }

    [Fact]
    public async Task UpdateIncidentStatusCommandHandler_WhenTransitionIsInvalid_DoesNotPublishRealtimeNotification()
    {
        var incident = Incident.Create("Audit status", "Invalid transition", IncidentSeverity.High, Guid.NewGuid());
        var dbContext = new FakeOperationsCenterDbContext(incident);
        var notifier = new FakeIncidentRealTimeNotifier();
        var handler = new UpdateIncidentStatusCommandHandler(dbContext, notifier, CreateTelemetry());

        UpdateIncidentStatusResult result = await handler.Handle(
            new UpdateIncidentStatusCommand(incident.Id, IncidentStatus.Closed, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(UpdateIncidentStatusOutcome.InvalidTransition, result.Outcome);
        Assert.Empty(notifier.StatusChangedMessages);
    }

    [Fact]
    public async Task UpdateIncidentStatusCommandHandler_WhenIncidentDoesNotExist_DoesNotPublishRealtimeNotification()
    {
        var dbContext = new FakeOperationsCenterDbContext();
        var notifier = new FakeIncidentRealTimeNotifier();
        var handler = new UpdateIncidentStatusCommandHandler(dbContext, notifier, CreateTelemetry());

        UpdateIncidentStatusResult result = await handler.Handle(
            new UpdateIncidentStatusCommand(Guid.NewGuid(), IncidentStatus.InProgress, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(UpdateIncidentStatusOutcome.NotFound, result.Outcome);
        Assert.Empty(notifier.StatusChangedMessages);
    }

    [Fact]
    public async Task UpdateIncidentStatusCommandHandler_WhenPersistenceFails_DoesNotPublishRealtimeNotification()
    {
        var incident = Incident.Create("Audit status", "Save fail", IncidentSeverity.High, Guid.NewGuid());
        var dbContext = new FakeOperationsCenterDbContext(incident)
        {
            ThrowOnSaveChanges = true
        };
        var notifier = new FakeIncidentRealTimeNotifier();
        var handler = new UpdateIncidentStatusCommandHandler(dbContext, notifier, CreateTelemetry());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new UpdateIncidentStatusCommand(incident.Id, IncidentStatus.InProgress, Guid.NewGuid()),
                CancellationToken.None));

        Assert.Empty(notifier.StatusChangedMessages);
    }

    private sealed class FakeOperationsCenterDbContext(Incident? existingIncident = null) : IOperationsCenterDbContext
    {
        private readonly Incident? _existingIncident = existingIncident;

        public bool ThrowOnSaveChanges { get; init; }

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

        public Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnSaveChanges)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            return Task.FromResult(1);
        }
    }

    private sealed class FakeIncidentRealTimeNotifier : IIncidentRealTimeNotifier
    {
        public List<IncidentCreatedMessage> CreatedMessages { get; } = [];

        public List<IncidentStatusChangedMessage> StatusChangedMessages { get; } = [];

        public Task IncidentCreatedAsync(IncidentCreatedMessage message, CancellationToken cancellationToken)
        {
            CreatedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task IncidentStatusChangedAsync(IncidentStatusChangedMessage message, CancellationToken cancellationToken)
        {
            StatusChangedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private static IOperationsCenterTelemetry CreateTelemetry() =>
        new OperationsCenterTelemetry(
            new ActivitySource(OperationsCenterTelemetry.ActivitySourceName),
            new Meter(OperationsCenterTelemetry.MeterName));
}
