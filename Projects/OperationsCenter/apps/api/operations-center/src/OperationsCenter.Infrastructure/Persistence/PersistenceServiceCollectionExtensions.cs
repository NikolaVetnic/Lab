using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OperationsCenter.Application.Persistence;

namespace OperationsCenter.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OperationsCenterDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'OperationsCenterDatabase' is not configured.");
        }

        services.AddDbContext<OperationsCenterDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IOperationsCenterDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<OperationsCenterDbContext>());

        return services;
    }
}
