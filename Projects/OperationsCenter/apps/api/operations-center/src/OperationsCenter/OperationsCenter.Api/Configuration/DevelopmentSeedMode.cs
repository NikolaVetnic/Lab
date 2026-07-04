using Microsoft.Extensions.Hosting;
using OperationsCenter.Infrastructure.Development;

namespace OperationsCenter.Api.Configuration;

internal static class DevelopmentSeedMode
{
    private const string SeedArgument = "--seed";
    private const string DemoSeedArgumentPrefix = "--seed=";
    private const string DemoSeedProfileValue = "demo";

    internal static bool IsSeedRequested(string[] args)
    {
        return ResolveRequestedProfile(args).HasValue;
    }

    internal static DevelopmentSeedProfile? ResolveRequestedProfile(string[] args)
    {
        foreach (var argument in args)
        {
            if (string.Equals(argument, SeedArgument, StringComparison.OrdinalIgnoreCase))
            {
                return DevelopmentSeedProfile.Standard;
            }

            if (argument.StartsWith(DemoSeedArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var profile = argument[DemoSeedArgumentPrefix.Length..];
                if (string.Equals(profile, DemoSeedProfileValue, StringComparison.OrdinalIgnoreCase))
                {
                    return DevelopmentSeedProfile.Demo;
                }
            }
        }

        return null;
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
