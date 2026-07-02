using Microsoft.Extensions.Hosting;

namespace OperationsCenter.Api.Configuration;

internal static class DevelopmentSeedMode
{
    private const string SeedArgument = "--seed";

    internal static bool IsSeedRequested(string[] args)
    {
        return args.Any(argument => string.Equals(argument, SeedArgument, StringComparison.OrdinalIgnoreCase));
    }

    internal static SeedEnvironmentValidationResult ValidateEnvironment(IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return SeedEnvironmentValidationResult.Valid();
        }

        return SeedEnvironmentValidationResult.Invalid(
            "Seed mode is allowed only in Development. Database changes were not applied.");
    }
}

internal readonly record struct SeedEnvironmentValidationResult(bool IsValid, string? ErrorMessage)
{
    public static SeedEnvironmentValidationResult Valid()
    {
        return new SeedEnvironmentValidationResult(true, null);
    }

    public static SeedEnvironmentValidationResult Invalid(string errorMessage)
    {
        return new SeedEnvironmentValidationResult(false, errorMessage);
    }
}
