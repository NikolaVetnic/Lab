using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.Contracts;

public sealed record UpdateIncidentStatusRequest(IncidentStatus Status);
