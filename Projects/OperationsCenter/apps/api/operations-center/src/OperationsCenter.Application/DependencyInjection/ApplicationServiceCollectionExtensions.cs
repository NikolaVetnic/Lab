using Microsoft.Extensions.DependencyInjection;
using OperationsCenter.Application.Common;

namespace OperationsCenter.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromAssemblyOf<IUseCase>()
            .AddClasses(classes => classes.AssignableTo<IUseCase>())
            .AsSelf()
            .WithScopedLifetime());

        return services;
    }
}
