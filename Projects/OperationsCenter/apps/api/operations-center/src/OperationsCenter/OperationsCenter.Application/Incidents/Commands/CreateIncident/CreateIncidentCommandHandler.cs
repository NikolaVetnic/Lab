using BuildingBlocks.Cqrs.Abstractions;
using OperationsCenter.Application.Incidents.Contracts;
using OperationsCenter.Application.Incidents.Realtime;
using OperationsCenter.Application.Observability;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Contracts.Realtime;
using OperationsCenter.Domain.Audit;
using OperationsCenter.Domain.Incidents;

namespace OperationsCenter.Application.Incidents.Commands.CreateIncident;

public sealed class CreateIncidentCommandHandler(
    IOperationsCenterDbContext dbContext,
    IIncidentRealTimeNotifier realTimeNotifier,
    IOperationsCenterTelemetry telemetry)
    : ICommandHandler<CreateIncidentCommand, IncidentResponse>
{
    private readonly IOperationsCenterDbContext _dbContext = dbContext;
    private readonly IIncidentRealTimeNotifier _realTimeNotifier = realTimeNotifier;
    private readonly IOperationsCenterTelemetry _telemetry = telemetry;

    public async Task<IncidentResponse> Handle(CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        using var activity = _telemetry.StartIncidentCreateActivity();

        Incident incident = Incident.Create(request.Title, request.Description, request.Severity, request.ActorUserId);

        activity?.SetTag("incident.id", incident.Id);
        activity?.SetTag("incident.severity", incident.Severity.ToString());

        AuditEvent auditEvent = AuditEvent.Create(
            entityType: "Incident",
            entityId: incident.Id,
            action: "Created",
            actorId: request.ActorUserId.ToString("D"));

        await _dbContext.AddIncidentAsync(incident, cancellationToken);
        await _dbContext.AddAuditEventAsync(auditEvent, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _telemetry.RecordIncidentCreated(incident.Severity.ToString());

        var createdMessage = new IncidentCreatedMessage(
            incident.Id,
            incident.Title,
            incident.Severity.ToString(),
            incident.Status.ToString(),
            incident.CreatedAt);

        await _realTimeNotifier.IncidentCreatedAsync(createdMessage, cancellationToken);

        return new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Severity,
            incident.Status,
            incident.CreatedAt);
    }
}
