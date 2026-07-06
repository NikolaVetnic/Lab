using Microsoft.EntityFrameworkCore;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Identity;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Infrastructure.Persistence;

public sealed class OperationsCenterDbContext(DbContextOptions<OperationsCenterDbContext> options)
    : DbContext(options), IOperationsCenterDbContext
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<User> Users => Set<User>();

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

    public Task<bool> IncidentExistsAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return Incidents
            .AsNoTracking()
            .AnyAsync(incident => incident.Id == incidentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken)
    {
        return await Incidents
            .AsNoTracking()
            .OrderByDescending(incident => incident.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> ListIncidentAuditEventsAsync(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        return await AuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.EntityType == "Incident" && auditEvent.EntityId == incidentId)
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ThenBy(auditEvent => auditEvent.Id)
            .Take(200)
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

    public async Task AddUserAsync(User user, CancellationToken cancellationToken)
    {
        await Users.AddAsync(user, cancellationToken);
    }

    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult<User?>(null);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        return Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsCenterDbContext).Assembly);
    }
}
