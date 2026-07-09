namespace OperationsCenter.Contracts.Realtime;

public sealed record IncidentStatusChangedMessage(
    Guid IncidentId,
    string PreviousStatus,
    string NewStatus,
    DateTimeOffset ChangedAt);
