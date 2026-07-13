using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OperationsCenter.Application.Observability;

/// <summary>
/// Central, application-owned telemetry primitives for Operations Center.
/// Defines a single <see cref="ActivitySource"/> and <see cref="Meter"/> so that
/// use cases never instantiate their own instrumentation objects. The names are
/// referenced by the API composition root when registering OpenTelemetry.
/// </summary>
public static class OperationsCenterTelemetry
{
    /// <summary>Stable name for the application-owned <see cref="ActivitySource"/>.</summary>
    public const string ActivitySourceName = "OperationsCenter";

    /// <summary>Stable name for the application-owned <see cref="Meter"/>.</summary>
    public const string MeterName = "OperationsCenter";

    /// <summary>Name of the counter incremented after an incident is persisted.</summary>
    public const string IncidentsCreatedCounterName = "operations_center.incidents.created";

    /// <summary>Name of the counter incremented after a successful status transition.</summary>
    public const string IncidentStatusChangesCounterName = "operations_center.incidents.status_changes";

    /// <summary>Name of the custom activity for incident creation.</summary>
    public const string IncidentCreateActivityName = "incident.create";

    /// <summary>Name of the custom activity for incident status changes.</summary>
    public const string IncidentStatusChangeActivityName = "incident.status_change";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> IncidentsCreatedCounter = Meter.CreateCounter<long>(
        IncidentsCreatedCounterName,
        unit: "{incident}",
        description: "Number of incidents successfully created.");

    private static readonly Counter<long> IncidentStatusChangesCounter = Meter.CreateCounter<long>(
        IncidentStatusChangesCounterName,
        unit: "{transition}",
        description: "Number of successful incident status transitions.");

    /// <summary>
    /// Records a successful incident creation. Must only be called after persistence succeeds.
    /// Uses <paramref name="severity"/> as a low-cardinality tag.
    /// </summary>
    public static void RecordIncidentCreated(string severity)
    {
        IncidentsCreatedCounter.Add(1, new KeyValuePair<string, object?>("severity", severity));
    }

    /// <summary>
    /// Records a successful incident status transition. Must only be called after persistence succeeds.
    /// Uses the previous and new status as low-cardinality tags.
    /// </summary>
    public static void RecordIncidentStatusChanged(string previousStatus, string newStatus)
    {
        IncidentStatusChangesCounter.Add(
            1,
            new KeyValuePair<string, object?>("previous_status", previousStatus),
            new KeyValuePair<string, object?>("new_status", newStatus));
    }
}
