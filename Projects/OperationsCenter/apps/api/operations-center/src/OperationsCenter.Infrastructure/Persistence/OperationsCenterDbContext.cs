using Microsoft.EntityFrameworkCore;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Infrastructure.Persistence;

public sealed class OperationsCenterDbContext(DbContextOptions<OperationsCenterDbContext> options)
    : DbContext(options), IOperationsCenterDbContext
{
    public DbSet<Incident> Incidents => Set<Incident>();

    public async Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken)
    {
        await Incidents.AddAsync(incident, cancellationToken);
    }

    public Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(incident => incident.Id == incidentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken)
    {
        return await Incidents
            .AsNoTracking()
            .OrderByDescending(incident => incident.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsCenterDbContext).Assembly);
    }
}
