using OperationsCenter.Application.Common;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Application.Incidents.UseCases;

public sealed class UpdateIncidentStatusUseCase(IOperationsCenterDbContext dbContext) : IUseCase
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;

    public async Task<UpdateIncidentStatusResult> ExecuteAsync(
        Guid incidentId,
        UpdateIncidentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await _dbContext.GetIncidentByIdForUpdateAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return UpdateIncidentStatusResult.NotFound;
        }

        var updated = incident.TryUpdateStatus(request.Status);
        if (!updated)
        {
            return UpdateIncidentStatusResult.InvalidTransition;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.CreatedAt);

        return UpdateIncidentStatusResult.Updated(response);
    }
}

public sealed record UpdateIncidentStatusResult
{
    private UpdateIncidentStatusResult(IncidentResponse? response, UpdateIncidentStatusOutcome outcome)
    {
        Response = response;
        Outcome = outcome;
    }

    public IncidentResponse? Response { get; }

    public UpdateIncidentStatusOutcome Outcome { get; }

    public static UpdateIncidentStatusResult NotFound { get; } = new(null, UpdateIncidentStatusOutcome.NotFound);

    public static UpdateIncidentStatusResult InvalidTransition { get; } = new(null, UpdateIncidentStatusOutcome.InvalidTransition);

    public static UpdateIncidentStatusResult Updated(IncidentResponse response) => new(response, UpdateIncidentStatusOutcome.Updated);
}

public enum UpdateIncidentStatusOutcome
{
    NotFound = 1,
    InvalidTransition = 2,
    Updated = 3
}
