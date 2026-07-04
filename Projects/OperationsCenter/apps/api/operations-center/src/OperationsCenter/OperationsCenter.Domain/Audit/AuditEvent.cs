namespace OperationsCenter.Domain.Audit;

public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        Guid id,
        string entityType,
        Guid entityId,
        string action,
        DateTimeOffset occurredAt,
        string? actorId,
        string? metadataJson)
    {
        Id = id;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        OccurredAt = occurredAt.ToUniversalTime();
        ActorId = actorId;
        MetadataJson = metadataJson;
    }

    public Guid Id { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public string? ActorId { get; private set; }

    public string? MetadataJson { get; private set; }

    public static AuditEvent Create(
        string entityType,
        Guid entityId,
        string action,
        DateTimeOffset? occurredAt = null,
        string? actorId = null,
        string? metadataJson = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("EntityType is required.", nameof(entityType));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action is required.", nameof(action));
        }

        var normalizedOccurredAt = (occurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime();

        return new AuditEvent(
            Guid.NewGuid(),
            entityType,
            entityId,
            action,
            normalizedOccurredAt,
            actorId,
            metadataJson);
    }
}
