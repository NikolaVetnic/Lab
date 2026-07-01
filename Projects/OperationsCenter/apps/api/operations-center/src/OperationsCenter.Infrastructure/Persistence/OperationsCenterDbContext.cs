using Microsoft.EntityFrameworkCore;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Infrastructure.Persistence;

public sealed class OperationsCenterDbContext(DbContextOptions<OperationsCenterDbContext> options)
    : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsCenterDbContext).Assembly);
    }
}
