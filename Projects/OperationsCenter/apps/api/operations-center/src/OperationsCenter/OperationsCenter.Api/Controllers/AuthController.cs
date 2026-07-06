using BuildingBlocks.Cqrs.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsCenter.Application.Identity.Commands.Login;
using OperationsCenter.Application.Identity.Contracts;

namespace OperationsCenter.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var command = new LoginCommand(request.Email!, request.Password!);
        LoginResult result = await sender.Send(command, cancellationToken);

        if (result.Outcome is LoginOutcome.InvalidCredentials)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid credentials.",
                Detail = "The provided email or password is incorrect.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (result.Outcome is LoginOutcome.UserInactive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "User is inactive.",
                Detail = "The account is inactive and cannot authenticate.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        return Ok(result.Response);
    }

    private static Dictionary<string, string[]> Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(request.Email)] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors[nameof(request.Password)] = ["Password is required."];
        }

        return errors;
    }
}
