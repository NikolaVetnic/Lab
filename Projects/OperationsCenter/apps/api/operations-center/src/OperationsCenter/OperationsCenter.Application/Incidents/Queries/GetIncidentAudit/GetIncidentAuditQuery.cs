using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Audits.Contracts;

namespace OperationsCenter.Application.Incidents.Queries.GetIncidentAudit;

public sealed record GetIncidentAuditQuery(Guid IncidentId) : IQuery<GetIncidentAuditResult>;

public sealed record GetIncidentAuditResult(
    IReadOnlyList<AuditEventResponse> Events,
    GetIncidentAuditOutcome Outcome)
{
    public static GetIncidentAuditResult NotFound { get; } = new([], GetIncidentAuditOutcome.NotFound);

    public static GetIncidentAuditResult Success(IReadOnlyList<AuditEventResponse> events)
    {
        return new(events, GetIncidentAuditOutcome.Success);
    }
}

public enum GetIncidentAuditOutcome
{
    NotFound = 1,
    Success = 2
}
