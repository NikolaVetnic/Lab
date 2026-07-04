using Microsoft.EntityFrameworkCore;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Infrastructure.Persistence;

public sealed class OperationsCenterDbContext(DbContextOptions<OperationsCenterDbContext> options)
    : DbContext(options), IOperationsCenterDbContext
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public async Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken)
    {
        await Incidents.AddAsync(incident, cancellationToken);
    }

    public async Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await AuditEvents.AddAsync(auditEvent, cancellationToken);
    }

    public Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(incident => incident.Id == incidentId, cancellationToken);
    }

    public Task<Incident?> GetIncidentByIdForUpdateAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return Incidents
            .FirstOrDefaultAsync(incident => incident.Id == incidentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken)
    {
        return await Incidents
            .AsNoTracking()
            .OrderByDescending(incident => incident.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
        string? entityType,
        Guid? entityId,
        string? action,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditEvent> query = AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(auditEvent => auditEvent.EntityType == entityType);
        }

        if (entityId.HasValue)
        {
            query = query.Where(auditEvent => auditEvent.EntityId == entityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(auditEvent => auditEvent.Action == action);
        }

        return await query
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsCenterDbContext).Assembly);
    }
}
