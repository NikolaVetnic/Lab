using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Application.Incidents.Queries.ListIncidents;

public sealed class ListIncidentsQueryHandler(IOperationsCenterDbContext dbContext)
    : IQueryHandler<ListIncidentsQuery, IReadOnlyList<IncidentResponse>>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<IncidentResponse>> Handle(ListIncidentsQuery request, CancellationToken cancellationToken)
    {
        var incidents = await _dbContext.ListIncidentsAsync(cancellationToken);

        return incidents
            .Select(incident => new IncidentResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity,
                incident.Status,
                incident.CreatedAt))
            .ToList();
    }
}
