using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.Commands.CreateIncident;

public sealed class CreateIncidentCommandHandler(IOperationsCenterDbContext dbContext)
    : ICommandHandler<CreateIncidentCommand, IncidentResponse>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<IncidentResponse> Handle(CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        Incident incident = Incident.Create(request.Title, request.Description, request.Severity);
        AuditEvent auditEvent = AuditEvent.Create(
            entityType: "Incident",
            entityId: incident.Id,
            action: "Created",
            actorId: request.ActorId);

        await _dbContext.AddIncidentAsync(incident, cancellationToken);
        await _dbContext.AddAuditEventAsync(auditEvent, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.CreatedAt);
    }
}
