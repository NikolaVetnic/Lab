using Microsoft.AspNetCore.Mvc;
using OperationsCenter.Infrastructure.Persistence;

namespace OperationsCenter.Api.Controllers;

[ApiController]
public sealed class HealthController(OperationsCenterDbContext dbContext) : ControllerBase
{
    private readonly OperationsCenterDbContext _dbContext = dbContext;

    [HttpGet("/health", Name = "GetHealth")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse("Healthy"));
    }

    [HttpGet("/ready", Name = "GetReadiness")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<object>> GetReadiness(CancellationToken cancellationToken)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails
                {
                    Title = "Database is not ready.",
                    Status = StatusCodes.Status503ServiceUnavailable
                });
        }

        return Ok(new { status = "Ready" });
    }
}

public sealed record HealthResponse(string Status);
