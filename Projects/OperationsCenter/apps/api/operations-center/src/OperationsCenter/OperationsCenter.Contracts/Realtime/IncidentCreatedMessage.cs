namespace OperationsCenter.Contracts.Realtime;

public sealed record IncidentCreatedMessage(
    Guid IncidentId,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset CreatedAt);
