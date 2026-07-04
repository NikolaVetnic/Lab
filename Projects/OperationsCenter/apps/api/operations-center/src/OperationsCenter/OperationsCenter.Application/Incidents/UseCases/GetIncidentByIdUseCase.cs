using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Application.Common;

namespace OperationsCenter.Application.Incidents.UseCases;

public sealed class GetIncidentByIdUseCase(IOperationsCenterDbContext dbContext) : IUseCase
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<IncidentResponse?> ExecuteAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.GetIncidentByIdAsync(incidentId, cancellationToken);

        if (incident is null)
        {
            return null;
        }

        return new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.CreatedAt);
    }
}
