namespace OperationsCenter.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
            .WithName("GetHealth")
            .WithTags("Health");

        return endpoints;
    }
}
