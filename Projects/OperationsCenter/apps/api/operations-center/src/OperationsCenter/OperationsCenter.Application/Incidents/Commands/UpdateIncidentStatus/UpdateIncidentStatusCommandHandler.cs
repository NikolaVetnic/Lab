using BuildingBlocks.Cqrs.Abstractions;
using System.Text.Json;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Incidents.Realtime;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Contracts.Realtime;
using OperationsCenter.Domain.Audit;

namespace OperationsCenter.Application.Incidents.Commands.UpdateIncidentStatus;

public sealed class UpdateIncidentStatusCommandHandler(
    IOperationsCenterDbContext dbContext,
    IIncidentRealTimeNotifier realTimeNotifier)
    : ICommandHandler<UpdateIncidentStatusCommand, UpdateIncidentStatusResult>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;
    private readonly IIncidentRealTimeNotifier _realTimeNotifier = realTimeNotifier;

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
            actorId: request.ActorUserId.ToString("D"),
            metadataJson: metadataJson);

        await _dbContext.AddAuditEventAsync(auditEvent, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var statusChangedMessage = new IncidentStatusChangedMessage(
            incident.Id,
            previousStatus.ToString(),
            incident.Status.ToString(),
            DateTimeOffset.UtcNow);

        await _realTimeNotifier.IncidentStatusChangedAsync(statusChangedMessage, cancellationToken);

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
