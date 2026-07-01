using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.UseCases;

public sealed class CreateIncidentUseCase(IOperationsCenterDbContext dbContext)
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<IncidentResponse> ExecuteAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        var incident = Incident.Create(request.Title!, request.Description, request.Severity);

        await _dbContext.AddIncidentAsync(incident, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.CreatedAt);
    }
}
