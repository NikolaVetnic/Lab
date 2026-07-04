using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Audits.Contracts;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Application.Audits.Queries.ListAudits;

public sealed class ListAuditsQueryHandler(IOperationsCenterDbContext dbContext)
    : IQueryHandler<ListAuditsQuery, IReadOnlyList<AuditEventResponse>>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<AuditEventResponse>> Handle(ListAuditsQuery request, CancellationToken cancellationToken)
    {
        var auditEvents = await _dbContext.ListAuditEventsAsync(
            request.EntityType,
            request.EntityId,
            request.Action,
            cancellationToken);

        return auditEvents
            .Select(auditEvent => new AuditEventResponse(
                auditEvent.Id,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.Action,
                auditEvent.OccurredAt,
                auditEvent.ActorId,
                auditEvent.MetadataJson))
            .ToList();
    }
}
