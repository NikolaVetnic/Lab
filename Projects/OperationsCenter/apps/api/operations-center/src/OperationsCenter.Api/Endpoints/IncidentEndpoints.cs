using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Incidents.UseCases;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Api.Endpoints;

public static class IncidentEndpoints
{
    public const string GetIncidentByIdEndpointName = "GetIncidentById";

    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var incidents = endpoints.MapGroup("/incidents").WithTags("Incidents");

        incidents.MapPost(
                string.Empty,
                async (
                    CreateIncidentRequest request,
                    CreateIncidentUseCase useCase,
                    ILoggerFactory loggerFactory,
                    CancellationToken cancellationToken) =>
                {
                    var validationErrors = ValidateCreateRequest(request);
                    if (validationErrors.Count > 0)
                    {
                        return Results.ValidationProblem(validationErrors);
                    }

                    var createdIncident = await useCase.ExecuteAsync(request, cancellationToken);

                    var logger = loggerFactory.CreateLogger("OperationsCenter.Api.Incidents");
                    logger.LogInformation(
                        "Incident {IncidentId} created with severity {Severity}",
                        createdIncident.Id,
                        createdIncident.Severity);

                    return Results.CreatedAtRoute(
                        GetIncidentByIdEndpointName,
                        new { id = createdIncident.Id },
                        createdIncident);
                })
            .WithName("CreateIncident")
            .WithSummary("Creates a new incident.")
            .Produces<IncidentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        incidents.MapGet(
                string.Empty,
                async (ListIncidentsUseCase useCase, CancellationToken cancellationToken) =>
                {
                    var response = await useCase.ExecuteAsync(cancellationToken);
                    return Results.Ok(response);
                })
            .WithName("ListIncidents")
            .WithSummary("Returns incidents ordered by newest creation time first.")
            .Produces<IReadOnlyList<IncidentResponse>>(StatusCodes.Status200OK);

        incidents.MapGet(
                "/{id:guid}",
                async (Guid id, GetIncidentByIdUseCase useCase, CancellationToken cancellationToken) =>
                {
                    var response = await useCase.ExecuteAsync(id, cancellationToken);

                    if (response is null)
                    {
                        return Results.Problem(
                            statusCode: StatusCodes.Status404NotFound,
                            title: "Incident not found.",
                            detail: $"Incident with id '{id}' does not exist.");
                    }

                    return Results.Ok(response);
                })
            .WithName(GetIncidentByIdEndpointName)
            .WithSummary("Returns a single incident by id.")
            .Produces<IncidentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
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
