using Microsoft.EntityFrameworkCore;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.Api.Endpoints;

public sealed record HealthResponse(string Status);

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthEndpoints();
        endpoints.MapIncidentEndpoints();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new HealthResponse("Healthy")))
            .WithName("GetHealth")
            .WithSummary("Returns the basic liveness status of the API.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
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
