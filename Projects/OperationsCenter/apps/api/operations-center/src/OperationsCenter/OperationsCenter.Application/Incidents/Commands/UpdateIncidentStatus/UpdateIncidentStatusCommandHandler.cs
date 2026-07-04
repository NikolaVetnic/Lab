using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Application.Incidents.Commands.UpdateIncidentStatus;

public sealed class UpdateIncidentStatusCommandHandler(IOperationsCenterDbContext dbContext)
    : ICommandHandler<UpdateIncidentStatusCommand, UpdateIncidentStatusResult>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<UpdateIncidentStatusResult> Handle(UpdateIncidentStatusCommand request, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.GetIncidentByIdForUpdateAsync(request.IncidentId, cancellationToken);
        if (incident is null)
        {
            return UpdateIncidentStatusResult.NotFound;
        }

        var updated = incident.TryUpdateStatus(request.Status);
        if (!updated)
        {
            return UpdateIncidentStatusResult.InvalidTransition;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.CreatedAt);

        return UpdateIncidentStatusResult.Updated(response);
    }
}
