using BuildingBlocks.Cqrs.Abstractions;
using System.Text.Json;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Audit;

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

        var previousStatus = incident.Status;
        var updated = incident.TryUpdateStatus(request.Status);
        if (!updated)
        {
            return UpdateIncidentStatusResult.InvalidTransition;
        }

        string metadataJson = JsonSerializer.Serialize(new
        {
            oldStatus = previousStatus.ToString(),
            newStatus = incident.Status.ToString()
        });

        AuditEvent auditEvent = AuditEvent.Create(
            entityType: "Incident",
            entityId: incident.Id,
            action: "StatusChanged",
            actorId: request.ActorId,
            metadataJson: metadataJson);

        await _dbContext.AddAuditEventAsync(auditEvent, cancellationToken);
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
