using Microsoft.AspNetCore.SignalR;
using OperationsCenter.Api.Hubs;
using OperationsCenter.Application.Incidents.Realtime;
using OperationsCenter.Contracts.Realtime;

namespace OperationsCenter.Api.Realtime;

public sealed class SignalRIncidentRealTimeNotifier(IHubContext<OperationsHub> hubContext)
    : IIncidentRealTimeNotifier
{
    private const string IncidentCreatedEvent = "IncidentCreated";
    private const string IncidentStatusChangedEvent = "IncidentStatusChanged";

    public Task IncidentCreatedAsync(IncidentCreatedMessage message, CancellationToken cancellationToken)
    {
        return hubContext.Clients.All.SendAsync(IncidentCreatedEvent, message, cancellationToken);
    }

    public Task IncidentStatusChangedAsync(IncidentStatusChangedMessage message, CancellationToken cancellationToken)
    {
        return hubContext.Clients.All.SendAsync(IncidentStatusChangedEvent, message, cancellationToken);
    }
}
