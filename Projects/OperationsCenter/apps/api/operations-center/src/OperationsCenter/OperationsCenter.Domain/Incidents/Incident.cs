namespace OperationsCenter.Domain.Incidents;

public sealed class Incident
{
    private static readonly IReadOnlyDictionary<IncidentStatus, IncidentStatus[]> AllowedStatusTransitions =
        new Dictionary<IncidentStatus, IncidentStatus[]>
        {
            [IncidentStatus.Open] = [IncidentStatus.InProgress, IncidentStatus.Resolved],
            [IncidentStatus.InProgress] = [IncidentStatus.Resolved],
            [IncidentStatus.Resolved] = [IncidentStatus.InProgress, IncidentStatus.Closed],
            [IncidentStatus.Closed] = []
        };

    private Incident()
    {
    }

    private Incident(Guid id, string title, string? description, IncidentSeverity severity, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        Severity = severity;
        Status = IncidentStatus.Open;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public IncidentSeverity Severity { get; private set; }

    public IncidentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Incident Create(string title, string? description, IncidentSeverity severity, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (title.Length > 200)
        {
            throw new ArgumentException("Title must not exceed 200 characters.", nameof(title));
        }

        if (description is not null && description.Length > 4000)
        {
            throw new ArgumentException("Description must not exceed 4000 characters.", nameof(description));
        }

        var normalizedCreatedAt = (createdAt ?? DateTimeOffset.UtcNow).ToUniversalTime();

        return new Incident(Guid.NewGuid(), title, description, severity, normalizedCreatedAt);
    }

    public bool TryUpdateStatus(IncidentStatus nextStatus)
    {
        if (!Enum.IsDefined(nextStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(nextStatus), "Status is invalid.");
        }

        if (!AllowedStatusTransitions.TryGetValue(Status, out var validNextStatuses))
        {
            return false;
        }

        if (!validNextStatuses.Contains(nextStatus))
        {
            return false;
        }

        Status = nextStatus;
        return true;
    }
}
