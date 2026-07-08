namespace OperationsCenter.Application.Audits.Contracts;

public sealed record AuditEventResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    DateTimeOffset OccurredAt,
    string? ActorId,
    string? ActorEmail,
    string? MetadataJson);
