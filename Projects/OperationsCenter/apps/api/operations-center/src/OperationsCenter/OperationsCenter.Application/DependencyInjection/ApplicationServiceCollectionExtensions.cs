using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Cqrs;
using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Commands.CreateIncident;
using OperationsCenter.Application.Observability;

namespace OperationsCenter.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IMediator, InProcessMediator>();
        services.AddScoped<ISender>(serviceProvider => serviceProvider.GetRequiredService<IMediator>());
        services.AddSingleton<IOperationsCenterTelemetry, OperationsCenterTelemetry>();

        services.Scan(scan => scan
            .FromAssemblyOf<CreateIncidentCommand>()
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IPipelineBehavior<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
