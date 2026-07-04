using Microsoft.EntityFrameworkCore;
using OperationsCenter.Infrastructure.Development;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.Api.Configuration;

public static class DevelopmentSeedModeExtensions
{
    public static async Task<bool> TryRunDevelopmentSeedAsync(this WebApplicationBuilder builder, string[] args)
    {
        var seedProfile = DevelopmentSeedMode.ResolveRequestedProfile(args);
        if (!seedProfile.HasValue)
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
        var insertedCount = await seeder.SeedAsync(seedProfile.Value);

        Console.WriteLine($"Development seed ({seedProfile.Value}) completed successfully. Added {insertedCount} incidents.");
        return true;
    }
}
