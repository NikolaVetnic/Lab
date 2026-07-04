using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Audits.Contracts;

namespace OperationsCenter.Application.Audits.Queries.ListAudits;

public sealed record ListAuditsQuery(
    string? EntityType,
    Guid? EntityId,
    string? Action) : IQuery<IReadOnlyList<AuditEventResponse>>;
