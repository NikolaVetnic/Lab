using Microsoft.Extensions.Hosting;
using OperationsCenter.Api.Configuration;
using OperationsCenter.Infrastructure.Development;

namespace OperationsCenter.UnitTests.Api.Configuration;

public sealed class DevelopmentSeedModeTests
{
    [Fact]
    public void ValidateEnvironment_WhenEnvironmentIsNotDevelopment_ReturnsInvalidResult()
    {
        var environment = new StubHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        var result = DevelopmentSeedMode.ValidateEnvironment(environment);

        Assert.False(result.IsValid);
        Assert.Equal(
            "Seed mode is allowed only in Development. Database changes were not applied.",
            result.ErrorMessage);
    }

    [Fact]
    public void IsSeedRequested_WhenSeedArgumentIsProvided_ReturnsTrue()
    {
        var requested = DevelopmentSeedMode.IsSeedRequested(["--seed"]);

        Assert.True(requested);
    }

    [Fact]
    public void ResolveRequestedProfile_WhenDemoArgumentIsProvided_ReturnsDemoProfile()
    {
        var profile = DevelopmentSeedMode.ResolveRequestedProfile(["--seed=demo"]);

        Assert.Equal(DevelopmentSeedProfile.Demo, profile);
    }

    [Fact]
    public void ResolveRequestedProfile_WhenSeedArgumentIsProvided_ReturnsStandardProfile()
    {
        var profile = DevelopmentSeedMode.ResolveRequestedProfile(["--seed"]);

        Assert.Equal(DevelopmentSeedProfile.Standard, profile);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "OperationsCenter.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
