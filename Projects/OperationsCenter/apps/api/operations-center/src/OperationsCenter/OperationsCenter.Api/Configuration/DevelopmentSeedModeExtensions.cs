using Microsoft.EntityFrameworkCore;
using OperationsCenter.Infrastructure.Development;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.Api.Configuration;

public static class DevelopmentSeedModeExtensions
{
    public static async Task<bool> TryRunDevelopmentSeedAsync(this WebApplicationBuilder builder, string[] args)
    {
        if (!DevelopmentSeedMode.IsSeedRequested(args))
        {
            return false;
        }

        var validationResult = DevelopmentSeedMode.ValidateEnvironment(builder.Environment);
        if (!validationResult.IsValid)
        {
            Console.Error.WriteLine(validationResult.ErrorMessage);
            Environment.ExitCode = 1;
            return true;
        }

        var seedingApp = builder.Build();

        using var scope = seedingApp.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OperationsCenterDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();

        await dbContext.Database.MigrateAsync();
        var insertedCount = await seeder.SeedAsync();

        Console.WriteLine($"Development seed completed successfully. Added {insertedCount} incidents.");
        return true;
    }
}
