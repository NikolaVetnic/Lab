using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Cqrs;
using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Common;

namespace OperationsCenter.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IMediator, InProcessMediator>();
        services.AddScoped<ISender>(serviceProvider => serviceProvider.GetRequiredService<IMediator>());

        services.Scan(scan => scan
            .FromAssemblyOf<IUseCase>()
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IPipelineBehavior<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblyOf<IUseCase>()
            .AddClasses(classes => classes.AssignableTo<IUseCase>())
            .AsSelf()
            .WithScopedLifetime());

        return services;
    }
}
