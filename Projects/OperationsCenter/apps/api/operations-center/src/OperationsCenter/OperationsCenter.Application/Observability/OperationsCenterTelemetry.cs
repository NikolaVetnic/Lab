using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OperationsCenter.Application.Observability;

public interface IOperationsCenterTelemetry
{
    Activity? StartIncidentCreateActivity();

    Activity? StartIncidentStatusChangeActivity();

    void RecordIncidentCreated(string severity);

    void RecordIncidentStatusChanged(string previousStatus, string newStatus);
}

/// <summary>
/// Central, application-owned telemetry primitives for Operations Center.
/// </summary>
public sealed class OperationsCenterTelemetry : IOperationsCenterTelemetry
{
    public const string ActivitySourceName = "OperationsCenter";

    public const string MeterName = "OperationsCenter";

    public const string IncidentsCreatedCounterName = "operations_center.incidents.created";

    public const string IncidentStatusChangesCounterName = "operations_center.incidents.status_changes";

    public const string IncidentCreateActivityName = "incident.create";

    public const string IncidentStatusChangeActivityName = "incident.status_change";

    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _incidentsCreatedCounter;
    private readonly Counter<long> _incidentStatusChangesCounter;

    public OperationsCenterTelemetry(IMeterFactory meterFactory)
        : this(new ActivitySource(ActivitySourceName), meterFactory.Create(MeterName))
    {
    }

    public OperationsCenterTelemetry(ActivitySource activitySource, Meter meter)
    {
        _activitySource = activitySource;
        _incidentsCreatedCounter = meter.CreateCounter<long>(
            IncidentsCreatedCounterName,
            unit: "incident",
            description: "Number of incidents successfully created.");
        _incidentStatusChangesCounter = meter.CreateCounter<long>(
            IncidentStatusChangesCounterName,
            unit: "transition",
            description: "Number of successful incident status transitions.");
    }

    public Activity? StartIncidentCreateActivity() =>
        _activitySource.StartActivity(IncidentCreateActivityName);

    public Activity? StartIncidentStatusChangeActivity() =>
        _activitySource.StartActivity(IncidentStatusChangeActivityName);

    public void RecordIncidentCreated(string severity)
    {
        _incidentsCreatedCounter.Add(1, new KeyValuePair<string, object?>("severity", severity));
    }

    public void RecordIncidentStatusChanged(string previousStatus, string newStatus)
    {
        _incidentStatusChangesCounter.Add(
            1,
            new KeyValuePair<string, object?>("previous_status", previousStatus),
            new KeyValuePair<string, object?>("new_status", newStatus));
    }
}
