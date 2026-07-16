using System.Diagnostics;
using System.Diagnostics.Metrics;
using OperationsCenter.Application.Incidents.Commands.CreateIncident;
using OperationsCenter.Application.Incidents.Commands.UpdateIncidentStatus;
using OperationsCenter.Application.Incidents.Realtime;
using OperationsCenter.Application.Observability;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Contracts.Realtime;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.UnitTests.Application.Observability;

[Collection(TelemetryTestCollection.Name)]
public sealed class IncidentTelemetryTests
{
    [Fact]
    public async Task CreateIncidentCommandHandler_WhenIncidentPersisted_IncrementsCreatedCounter()
    {
        using var recorder = new MeasurementRecorder();
        var telemetry = CreateTelemetry();
        var handler = new CreateIncidentCommandHandler(new FakeDbContext(), new FakeNotifier(), telemetry);

        await handler.Handle(
            new CreateIncidentCommand("Telemetry create", "desc", IncidentSeverity.High, Guid.NewGuid()),
            CancellationToken.None);

        Measurement measurement = Assert.Single(
            recorder.For(OperationsCenterTelemetry.IncidentsCreatedCounterName));
        Assert.Equal(1, measurement.Value);
        Assert.Equal("High", measurement.Tags["severity"]);
    }

    [Fact]
    public async Task CreateIncidentCommandHandler_WhenIncidentInvalid_DoesNotIncrementCreatedCounter()
    {
        using var recorder = new MeasurementRecorder();
        var telemetry = CreateTelemetry();
        var handler = new CreateIncidentCommandHandler(new FakeDbContext(), new FakeNotifier(), telemetry);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new CreateIncidentCommand(string.Empty, "desc", IncidentSeverity.High, Guid.NewGuid()),
                CancellationToken.None));

        Assert.Empty(recorder.For(OperationsCenterTelemetry.IncidentsCreatedCounterName));
    }

    [Fact]
    public async Task CreateIncidentCommandHandler_WhenPersistenceFails_DoesNotIncrementCreatedCounter()
    {
        using var recorder = new MeasurementRecorder();
        var telemetry = CreateTelemetry();
        var handler = new CreateIncidentCommandHandler(
            new FakeDbContext { ThrowOnSaveChanges = true },
            new FakeNotifier(),
            telemetry);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new CreateIncidentCommand("Telemetry create", "desc", IncidentSeverity.Medium, Guid.NewGuid()),
                CancellationToken.None));

        Assert.Empty(recorder.For(OperationsCenterTelemetry.IncidentsCreatedCounterName));
    }

    [Fact]
    public async Task UpdateIncidentStatusCommandHandler_WhenTransitionSucceeds_IncrementsStatusChangeCounter()
    {
        var incident = Incident.Create("Telemetry status", "desc", IncidentSeverity.High, Guid.NewGuid());
        using var recorder = new MeasurementRecorder();
        var telemetry = CreateTelemetry();
        var handler = new UpdateIncidentStatusCommandHandler(new FakeDbContext(incident), new FakeNotifier(), telemetry);

        UpdateIncidentStatusResult result = await handler.Handle(
            new UpdateIncidentStatusCommand(incident.Id, IncidentStatus.InProgress, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(UpdateIncidentStatusOutcome.Updated, result.Outcome);

        Measurement measurement = Assert.Single(
            recorder.For(OperationsCenterTelemetry.IncidentStatusChangesCounterName));
        Assert.Equal(1, measurement.Value);
        Assert.Equal("Open", measurement.Tags["previous_status"]);
        Assert.Equal("InProgress", measurement.Tags["new_status"]);
    }

    [Fact]
    public async Task UpdateIncidentStatusCommandHandler_WhenTransitionInvalid_DoesNotIncrementStatusChangeCounter()
    {
        var incident = Incident.Create("Telemetry status", "desc", IncidentSeverity.High, Guid.NewGuid());
        using var recorder = new MeasurementRecorder();
        var telemetry = CreateTelemetry();
        var handler = new UpdateIncidentStatusCommandHandler(new FakeDbContext(incident), new FakeNotifier(), telemetry);

        UpdateIncidentStatusResult result = await handler.Handle(
            new UpdateIncidentStatusCommand(incident.Id, IncidentStatus.Closed, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(UpdateIncidentStatusOutcome.InvalidTransition, result.Outcome);
        Assert.Empty(recorder.For(OperationsCenterTelemetry.IncidentStatusChangesCounterName));
    }

    private sealed record Measurement(long Value, IReadOnlyDictionary<string, object?> Tags);

    /// <summary>
    /// Captures measurements recorded against the application-owned meter for the duration of a test.
    /// </summary>
    private sealed class MeasurementRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<(string Name, Measurement Measurement)> _measurements = [];
        private readonly Lock _gate = new();

        public MeasurementRecorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == OperationsCenterTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var tagDictionary = new Dictionary<string, object?>();
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    tagDictionary[tag.Key] = tag.Value;
                }

                lock (_gate)
                {
                    _measurements.Add((instrument.Name, new Measurement(value, tagDictionary)));
                }
            });

            _listener.Start();
        }

        public IReadOnlyList<Measurement> For(string instrumentName)
        {
            lock (_gate)
            {
                return _measurements
                    .Where(entry => entry.Name == instrumentName)
                    .Select(entry => entry.Measurement)
                    .ToList();
            }
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed class FakeNotifier : IIncidentRealTimeNotifier
    {
        public Task IncidentCreatedAsync(IncidentCreatedMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task IncidentStatusChangedAsync(IncidentStatusChangedMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static IOperationsCenterTelemetry CreateTelemetry() =>
        new OperationsCenterTelemetry(
            new ActivitySource(OperationsCenterTelemetry.ActivitySourceName),
            new Meter(OperationsCenterTelemetry.MeterName));

    private sealed class FakeDbContext(Incident? existingIncident = null) : IOperationsCenterDbContext
    {
        private readonly Incident? _existingIncident = existingIncident;

        public bool ThrowOnSaveChanges { get; init; }

        public Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken) =>
            Task.FromResult(_existingIncident?.Id == incidentId ? _existingIncident : null);

        public Task<Incident?> GetIncidentByIdForUpdateAsync(Guid incidentId, CancellationToken cancellationToken) =>
            Task.FromResult(_existingIncident?.Id == incidentId ? _existingIncident : null);

        public Task<bool> IncidentExistsAsync(Guid incidentId, CancellationToken cancellationToken) =>
            Task.FromResult(_existingIncident is not null && _existingIncident.Id == incidentId);

        public Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Incident>>([]);

        public Task<IReadOnlyList<AuditEvent>> ListIncidentAuditEventsAsync(Guid incidentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>([]);

        public Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
            string? entityType,
            Guid? entityId,
            string? action,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEvent>>([]);

        public Task AddUserAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnSaveChanges)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            return Task.FromResult(1);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryTestCollection
{
    public const string Name = "OperationsCenter telemetry collection";
}
