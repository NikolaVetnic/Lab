using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;

namespace OperationsCenter.Application.Incidents.Queries.ListIncidents;

public sealed record ListIncidentsQuery : IQuery<IReadOnlyList<IncidentResponse>>;
