using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.Commands.UpdateIncidentStatus;

public sealed record UpdateIncidentStatusCommand(Guid IncidentId, IncidentStatus Status, Guid ActorUserId)
    : ICommand<UpdateIncidentStatusResult>;

public sealed record UpdateIncidentStatusResult(IncidentResponse? Response, UpdateIncidentStatusOutcome Outcome)
{
    public static UpdateIncidentStatusResult NotFound { get; } = new(null, UpdateIncidentStatusOutcome.NotFound);

    public static UpdateIncidentStatusResult InvalidTransition { get; } = new(null, UpdateIncidentStatusOutcome.InvalidTransition);

    public static UpdateIncidentStatusResult Updated(IncidentResponse response) => new(response, UpdateIncidentStatusOutcome.Updated);
}

public enum UpdateIncidentStatusOutcome
{
    NotFound = 1,
    InvalidTransition = 2,
    Updated = 3
}
