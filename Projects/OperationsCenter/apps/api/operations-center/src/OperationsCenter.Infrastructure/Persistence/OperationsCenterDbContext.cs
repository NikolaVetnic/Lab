using Microsoft.EntityFrameworkCore;

namespace OperationsCenter.Infrastructure.Persistence;

public sealed class OperationsCenterDbContext(DbContextOptions<OperationsCenterDbContext> options)
    : DbContext(options)
{
}
