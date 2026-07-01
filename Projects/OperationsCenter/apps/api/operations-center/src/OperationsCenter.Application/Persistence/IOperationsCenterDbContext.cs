using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Persistence;

public interface IOperationsCenterDbContext
{
    Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken);

    Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
