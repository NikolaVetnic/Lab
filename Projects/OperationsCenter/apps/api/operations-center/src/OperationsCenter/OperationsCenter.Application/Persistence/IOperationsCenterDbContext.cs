using OperationsCenter.Domain.Incidents;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Identity;

namespace OperationsCenter.Application.Persistence;

public interface IOperationsCenterDbContext
{
    Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken);

    Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<Incident?> GetIncidentByIdForUpdateAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<bool> IncidentExistsAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEvent>> ListIncidentAuditEventsAsync(
        Guid incidentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
        string? entityType,
        Guid? entityId,
        string? action,
        CancellationToken cancellationToken);

    Task AddUserAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
