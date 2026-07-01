using Microsoft.EntityFrameworkCore;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
            .WithName("GetHealth")
            .WithTags("Health");

        endpoints.MapGet(
                "/ready",
                async (OperationsCenterDbContext dbContext, CancellationToken cancellationToken) =>
                {
                    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
                    return canConnect
                        ? Results.Ok(new { status = "Ready" })
                        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database is not ready.");
                })
            .WithName("GetReadiness")
            .WithTags("Health");

        return endpoints;
    }
}
