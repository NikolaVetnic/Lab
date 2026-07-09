using OperationsCenter.Contracts.Realtime;

namespace OperationsCenter.Application.Incidents.Realtime;

public interface IIncidentRealTimeNotifier
{
    Task IncidentCreatedAsync(IncidentCreatedMessage message, CancellationToken cancellationToken);

    Task IncidentStatusChangedAsync(IncidentStatusChangedMessage message, CancellationToken cancellationToken);
}
