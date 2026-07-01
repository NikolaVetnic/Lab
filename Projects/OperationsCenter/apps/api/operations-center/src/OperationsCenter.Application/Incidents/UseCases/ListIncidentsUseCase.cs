using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Application.Common;

namespace OperationsCenter.Application.Incidents.UseCases;

public sealed class ListIncidentsUseCase(IOperationsCenterDbContext dbContext) : IUseCase
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<IncidentResponse>> ExecuteAsync(CancellationToken cancellationToken)
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
