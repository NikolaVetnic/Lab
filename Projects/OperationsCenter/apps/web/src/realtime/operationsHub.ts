import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

export const incidentCreatedEventName = 'IncidentCreated';
export const incidentStatusChangedEventName = 'IncidentStatusChanged';

export type LiveConnectionStatus = 'connected' | 'reconnecting' | 'disconnected';

export interface IncidentCreatedRealtimeMessage {
  incidentId: string;
  title: string;
  severity: string;
  status: string;
  createdAt: string;
}

export interface IncidentStatusChangedRealtimeMessage {
  incidentId: string;
  previousStatus: string;
  newStatus: string;
  changedAt: string;
}

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

function getOperationsHubUrl(): string {
  if (import.meta.env.DEV) {
    return '/api/hubs/operations';
  }

  if (!configuredBaseUrl) {
    return '/hubs/operations';
  }

  return `${configuredBaseUrl.replace(/\/+$/, '')}/hubs/operations`;
}

export function createOperationsHubConnection(
  accessTokenFactory: () => string | undefined,
): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(getOperationsHubUrl(), {
      accessTokenFactory: () => accessTokenFactory() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
