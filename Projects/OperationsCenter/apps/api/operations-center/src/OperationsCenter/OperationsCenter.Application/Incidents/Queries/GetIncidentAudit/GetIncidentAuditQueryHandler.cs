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
        var actorIdToEmail = await ResolveActorEmailsAsync(auditEvents, cancellationToken);

        var response = auditEvents
            .Select(auditEvent => new AuditEventResponse(
                auditEvent.Id,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.Action,
                auditEvent.OccurredAt,
                auditEvent.ActorId,
                TryGetActorEmail(auditEvent.ActorId, actorIdToEmail),
                auditEvent.MetadataJson))
            .ToList();

        return GetIncidentAuditResult.Success(response);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveActorEmailsAsync(
        IReadOnlyList<Domain.Audit.AuditEvent> auditEvents,
        CancellationToken cancellationToken)
    {
        var actorIds = auditEvents
            .Select(auditEvent => auditEvent.ActorId)
            .Where(actorId => Guid.TryParse(actorId, out _))
            .Select(actorId => Guid.Parse(actorId!))
            .Distinct()
            .ToArray();

        return await _dbContext.GetUserEmailsByIdsAsync(actorIds, cancellationToken);
    }

    private static string? TryGetActorEmail(string? actorId, IReadOnlyDictionary<Guid, string> actorIdToEmail)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        if (!Guid.TryParse(actorId, out var userId))
        {
            return actorId.Contains('@') ? actorId : null;
        }

        return actorIdToEmail.TryGetValue(userId, out var email) ? email : null;
    }
}
