using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.Commands.CreateIncident;

public sealed record CreateIncidentCommand(
    string Title,
    string? Description,
    IncidentSeverity Severity,
    string? ActorId = null) : ICommand<IncidentResponse>;
