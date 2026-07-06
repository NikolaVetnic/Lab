using BuildingBlocks.Cqrs.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Application.Incidents.Commands.CreateIncident;
using OperationsCenter.Application.Incidents.Commands.UpdateIncidentStatus;
using OperationsCenter.Application.Incidents.Queries.GetIncidentById;
using OperationsCenter.Application.Incidents.Queries.ListIncidents;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Api.Controllers;

[ApiController]
[Route("incidents")]
public sealed class IncidentsController(
    ISender sender,
    ILogger<IncidentsController> logger,
    ICurrentUser currentUser) : ControllerBase
{
    private const string GetIncidentByIdRouteName = "GetIncidentById";

    [HttpGet]
    [Authorize(Policy = "Incidents.Read")]
    [ProducesResponseType<IReadOnlyList<IncidentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncidentResponse>>> ListIncidentsAsync(CancellationToken cancellationToken)
    {
        var query = new ListIncidentsQuery();
        IReadOnlyList<IncidentResponse> response = await sender.Send(query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}", Name = GetIncidentByIdRouteName)]
    [Authorize(Policy = "Incidents.Read")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentResponse>> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetIncidentByIdQuery(id);
        GetIncidentByIdResult result = await sender.Send(query, cancellationToken);

        if (result.Response is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Incident not found.",
                Detail = $"Incident with id '{id}' does not exist.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result.Response);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Incidents.Write")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IncidentResponse>> UpdateIncidentStatusAsync(
        Guid id,
        [FromBody] UpdateIncidentStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(IncidentStatus), request.Status))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Status)] = ["Status is required and must be a valid value."]
            }));
        }

        var command = new UpdateIncidentStatusCommand(id, request.Status, ResolveActorId());
        UpdateIncidentStatusResult result = await sender.Send(command, cancellationToken);

        if (result.Outcome is UpdateIncidentStatusOutcome.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Incident not found.",
                Detail = $"Incident with id '{id}' does not exist.",
                Status = StatusCodes.Status404NotFound
            });
        }

        if (result.Outcome is UpdateIncidentStatusOutcome.InvalidTransition)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Invalid incident status transition.",
                Detail = $"Incident with id '{id}' cannot transition to status '{request.Status}'.",
                Status = StatusCodes.Status409Conflict
            });
        }

        logger.LogInformation(
            "Incident {IncidentId} updated to status {Status}",
            id,
            request.Status);

        return Ok(result.Response);
    }

    [HttpPost]
    [Authorize(Policy = "Incidents.Write")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentResponse>> CreateIncidentAsync(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        var command = new CreateIncidentCommand(request.Title!, request.Description, request.Severity, ResolveActorId());
        IncidentResponse createdIncident = await sender.Send(command, cancellationToken);

        logger.LogInformation(
            "Incident {IncidentId} created with severity {Severity}",
            createdIncident.Id,
            createdIncident.Severity);

        return CreatedAtRoute(
            GetIncidentByIdRouteName,
            new { id = createdIncident.Id },
            createdIncident);
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateIncidentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["Title is required."];
        }
        else if (request.Title.Length > 200)
        {
            errors[nameof(request.Title)] = ["Title must not exceed 200 characters."];
        }

        if (request.Description is not null && request.Description.Length > 4000)
        {
            errors[nameof(request.Description)] = ["Description must not exceed 4000 characters."];
        }

        if (!Enum.IsDefined(typeof(IncidentSeverity), request.Severity))
        {
            errors[nameof(request.Severity)] = ["Severity is required and must be a valid value."];
        }

        return errors;
    }

    private string? ResolveActorId()
    {
        if (currentUser.UserId.HasValue)
        {
            return currentUser.UserId.Value.ToString();
        }

        return currentUser.Email;
    }
}
