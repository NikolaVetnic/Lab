using Microsoft.Extensions.DependencyInjection;
using OperationsCenter.Application.DependencyInjection;
using OperationsCenter.Application.Incidents.UseCases;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.UnitTests.Application.DependencyInjection;

public sealed class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApplicationServices_WhenCalled_RegistersUseCaseAsSelf()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOperationsCenterDbContext, FakeOperationsCenterDbContext>();

        services.AddApplicationServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolvedUseCase = scope.ServiceProvider.GetService<CreateIncidentUseCase>();

        Assert.NotNull(resolvedUseCase);
    }

    private sealed class FakeOperationsCenterDbContext : IOperationsCenterDbContext
    {
        public Task AddIncidentAsync(Incident incident, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<Incident?> GetIncidentByIdAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Incident?>(null);
        }

        public Task<Incident?> GetIncidentByIdForUpdateAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Incident?>(null);
        }

        public Task<IReadOnlyList<Incident>> ListIncidentsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Incident>>(Array.Empty<Incident>());
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
