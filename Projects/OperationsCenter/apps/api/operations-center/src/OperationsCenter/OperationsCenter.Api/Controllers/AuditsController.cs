using BuildingBlocks.Cqrs.Abstractions;
using Microsoft.AspNetCore.Mvc;
using OperationsCenter.Application.Audits.Contracts;
using OperationsCenter.Application.Audits.Queries.ListAudits;

namespace OperationsCenter.Api.Controllers;

[ApiController]
public sealed class AuditsController(ISender sender) : ControllerBase
{
    [HttpGet("/audits")]
    [ProducesResponseType<IReadOnlyList<AuditEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEventResponse>>> ListAuditsAsync(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? action,
        CancellationToken cancellationToken)
    {
        var query = new ListAuditsQuery(entityType, entityId, action);
        IReadOnlyList<AuditEventResponse> audits = await sender.Send(query, cancellationToken);
        return Ok(audits);
    }

    [HttpGet("/incidents/{incidentId:guid}/audits")]
    [ProducesResponseType<IReadOnlyList<AuditEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEventResponse>>> ListIncidentAuditsAsync(
        Guid incidentId,
        [FromQuery] string? action,
        CancellationToken cancellationToken)
    {
        var query = new ListAuditsQuery("Incident", incidentId, action);
        IReadOnlyList<AuditEventResponse> audits = await sender.Send(query, cancellationToken);
        return Ok(audits);
    }
}
