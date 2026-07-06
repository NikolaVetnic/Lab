using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Audits.Contracts;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Application.Incidents.Queries.GetIncidentAudit;

public sealed class GetIncidentAuditQueryHandler(IOperationsCenterDbContext dbContext)
    : IQueryHandler<GetIncidentAuditQuery, GetIncidentAuditResult>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<GetIncidentAuditResult> Handle(GetIncidentAuditQuery request, CancellationToken cancellationToken)
    {
        var incidentExists = await _dbContext.IncidentExistsAsync(request.IncidentId, cancellationToken);
        if (!incidentExists)
        {
            return GetIncidentAuditResult.NotFound;
        }

        var auditEvents = await _dbContext.ListIncidentAuditEventsAsync(request.IncidentId, cancellationToken);

        var response = auditEvents
            .Select(auditEvent => new AuditEventResponse(
                auditEvent.Id,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.Action,
                auditEvent.OccurredAt,
                auditEvent.ActorId,
                auditEvent.MetadataJson))
            .ToList();

        return GetIncidentAuditResult.Success(response);
    }
}
