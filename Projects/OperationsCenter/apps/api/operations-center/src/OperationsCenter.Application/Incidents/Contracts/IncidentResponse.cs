using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.Contracts;

public sealed record IncidentResponse(
    Guid Id,
    string Title,
    string? Description,
    IncidentSeverity Severity,
    IncidentStatus Status,
    DateTimeOffset CreatedAt);
