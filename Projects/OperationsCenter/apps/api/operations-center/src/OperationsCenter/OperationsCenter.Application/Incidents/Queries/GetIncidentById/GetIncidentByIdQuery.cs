using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;

namespace OperationsCenter.Application.Incidents.Queries.GetIncidentById;

public sealed record GetIncidentByIdQuery(Guid IncidentId) : IQuery<GetIncidentByIdResult>;

public sealed record GetIncidentByIdResult(IncidentResponse? Response);
