using Microsoft.EntityFrameworkCore;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.Api.Configuration;

public static class MigrationModeExtensions
{
    private const string MigrateArgument = "--migrate";

    public static async Task<bool> TryRunMigrationAsync(this WebApplicationBuilder builder, string[] args)
    {
        if (!args.Any(argument => string.Equals(argument, MigrateArgument, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var migrationApp = builder.Build();

        using var scope = migrationApp.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OperationsCenterDbContext>();

        await dbContext.Database.MigrateAsync();

        Console.WriteLine("Database migrations completed successfully.");
        return true;
    }
}
