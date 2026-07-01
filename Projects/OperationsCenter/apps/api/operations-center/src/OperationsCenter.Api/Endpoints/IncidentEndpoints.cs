using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Incidents.UseCases;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Api.Endpoints;

public static class IncidentEndpoints
{
    public const string GetIncidentByIdEndpointName = "GetIncidentById";
    private const string LoggerCategory = "OperationsCenter.Api.Incidents";

    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var incidents = endpoints.MapGroup("/incidents").WithTags("Incidents");

        MapCreateIncidentEndpoint(incidents);
        MapListIncidentsEndpoint(incidents);
        MapGetIncidentByIdEndpoint(incidents);
        MapUpdateIncidentStatusEndpoint(incidents);

        return endpoints;
    }

    private static void MapCreateIncidentEndpoint(RouteGroupBuilder incidents)
    {
        incidents.MapPost(string.Empty, CreateIncidentAsync)
            .WithName("CreateIncident")
            .WithSummary("Creates a new incident.")
            .Produces<IncidentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapListIncidentsEndpoint(RouteGroupBuilder incidents)
    {
        incidents.MapGet(string.Empty, ListIncidentsAsync)
            .WithName("ListIncidents")
            .WithSummary("Returns incidents ordered by newest creation time first.")
            .Produces<IReadOnlyList<IncidentResponse>>(StatusCodes.Status200OK);
    }

    private static void MapGetIncidentByIdEndpoint(RouteGroupBuilder incidents)
    {
        incidents.MapGet("/{id:guid}", GetIncidentByIdAsync)
            .WithName(GetIncidentByIdEndpointName)
            .WithSummary("Returns a single incident by id.")
            .Produces<IncidentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapUpdateIncidentStatusEndpoint(RouteGroupBuilder incidents)
    {
        incidents.MapPatch("/{id:guid}/status", UpdateIncidentStatusAsync)
            .WithName("UpdateIncidentStatus")
            .WithSummary("Updates the status of an existing incident.")
            .Produces<IncidentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateIncidentAsync(
        CreateIncidentRequest request,
        CreateIncidentUseCase useCase,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateCreateRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var createdIncident = await useCase.ExecuteAsync(request, cancellationToken);

        var logger = loggerFactory.CreateLogger(LoggerCategory);
        logger.LogInformation(
            "Incident {IncidentId} created with severity {Severity}",
            createdIncident.Id,
            createdIncident.Severity);

        return Results.CreatedAtRoute(
            GetIncidentByIdEndpointName,
            new { id = createdIncident.Id },
            createdIncident);
    }

    private static async Task<IResult> ListIncidentsAsync(
        ListIncidentsUseCase useCase,
        CancellationToken cancellationToken)
    {
        var response = await useCase.ExecuteAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetIncidentByIdAsync(
        Guid id,
        GetIncidentByIdUseCase useCase,
        CancellationToken cancellationToken)
    {
        var response = await useCase.ExecuteAsync(id, cancellationToken);
        return response is null
            ? IncidentNotFound(id)
            : Results.Ok(response);
    }

    private static async Task<IResult> UpdateIncidentStatusAsync(
        Guid id,
        UpdateIncidentStatusRequest request,
        UpdateIncidentStatusUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(IncidentStatus), request.Status))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Status)] = ["Status is required and must be a valid value."]
            });
        }

        var result = await useCase.ExecuteAsync(id, request, cancellationToken);

        return result.Outcome switch
        {
            UpdateIncidentStatusOutcome.NotFound => IncidentNotFound(id),
            UpdateIncidentStatusOutcome.InvalidTransition => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Invalid incident status transition.",
                detail: $"Incident with id '{id}' cannot transition to status '{request.Status}'."),
            _ => Results.Ok(result.Response)
        };
    }

    private static IResult IncidentNotFound(Guid id)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Incident not found.",
            detail: $"Incident with id '{id}' does not exist.");
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
}
