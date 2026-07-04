using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.Contracts;

public sealed record CreateIncidentRequest(string? Title, string? Description, IncidentSeverity Severity);
