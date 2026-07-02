using Microsoft.Extensions.Hosting;
using OperationsCenter.Api.Configuration;

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

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "OperationsCenter.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
