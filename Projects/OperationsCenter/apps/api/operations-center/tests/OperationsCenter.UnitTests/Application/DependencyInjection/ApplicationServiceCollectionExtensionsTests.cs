using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.DependencyInjection;
using OperationsCenter.Application.Incidents.UseCases;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Incidents;
using System.Collections.Concurrent;

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

    [Fact]
    public void AddApplicationServices_WhenCalled_RegistersMediator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOperationsCenterDbContext, FakeOperationsCenterDbContext>();

        services.AddApplicationServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var mediator = scope.ServiceProvider.GetService<IMediator>();

        Assert.NotNull(mediator);
    }

    [Fact]
    public async Task AddApplicationServices_WhenCommandHandlerRegistered_CommandIsDispatchedViaMediator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOperationsCenterDbContext, FakeOperationsCenterDbContext>();

        services.AddApplicationServices();
        services.AddScoped<ICommandHandler<TestCommand, int>, TestCommandHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new TestCommand(21));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task AddApplicationServices_WhenQueryHandlerRegistered_QueryIsDispatchedViaMediator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOperationsCenterDbContext, FakeOperationsCenterDbContext>();

        services.AddApplicationServices();
        services.AddScoped<IQueryHandler<TestQuery, int>, TestQueryHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        int result = await mediator.Send(new TestQuery(12));

        Assert.Equal(17, result);
    }

    [Fact]
    public async Task AddApplicationServices_WhenPipelineBehaviorRegistered_BehaviorWrapsHandler()
    {
        var callOrder = new ConcurrentQueue<string>();

        var services = new ServiceCollection();
        services.AddSingleton<IOperationsCenterDbContext, FakeOperationsCenterDbContext>();
        services.AddSingleton(callOrder);

        services.AddApplicationServices();
        services.AddScoped<ICommandHandler<TestPipelineCommand, int>, TestPipelineCommandHandler>();
        services.AddScoped<IPipelineBehavior<TestPipelineCommand, int>, RecordingPipelineBehavior>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        int result = await mediator.Send(new TestPipelineCommand(10));

        Assert.Equal(21, result);
        Assert.Equal(["behavior:before", "handler", "behavior:after"], callOrder.ToArray());
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

    private sealed record TestCommand(int Value) : ICommand<int>;

    private sealed record TestQuery(int Value) : IQuery<int>;

    private sealed record TestPipelineCommand(int Value) : ICommand<int>;

    private sealed class TestCommandHandler : ICommandHandler<TestCommand, int>
    {
        public Task<int> Handle(TestCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value * 2);
        }
    }

    private sealed class TestQueryHandler : IQueryHandler<TestQuery, int>
    {
        public Task<int> Handle(TestQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value + 5);
        }
    }

    private sealed class TestPipelineCommandHandler(ConcurrentQueue<string> callOrder) : ICommandHandler<TestPipelineCommand, int>
    {
        public Task<int> Handle(TestPipelineCommand request, CancellationToken cancellationToken)
        {
            callOrder.Enqueue("handler");
            return Task.FromResult(request.Value * 2);
        }
    }

    private sealed class RecordingPipelineBehavior(ConcurrentQueue<string> callOrder)
        : IPipelineBehavior<TestPipelineCommand, int>
    {
        public async Task<int> Handle(
            TestPipelineCommand request,
            RequestHandlerDelegate<int> next,
            CancellationToken cancellationToken)
        {
            callOrder.Enqueue("behavior:before");
            int value = await next();
            callOrder.Enqueue("behavior:after");
            return value + 1;
        }
    }
}
