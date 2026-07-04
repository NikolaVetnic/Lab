using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Application.Incidents.Queries.GetIncidentById;

public sealed class GetIncidentByIdQueryHandler(IOperationsCenterDbContext dbContext)
    : IQueryHandler<GetIncidentByIdQuery, GetIncidentByIdResult>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<GetIncidentByIdResult> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.GetIncidentByIdAsync(request.IncidentId, cancellationToken);
        if (incident is null)
        {
            return new GetIncidentByIdResult(null);
        }

        var response = new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.CreatedAt);

        return new GetIncidentByIdResult(response);
    }
}
