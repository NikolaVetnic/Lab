import { apiRequest } from './apiClient';

export type IncidentStatus = 1 | 2 | 3 | 4;

export interface IncidentStatusOption {
  value: IncidentStatus;
  label: string;
}

export const severityLabels: Record<number, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
};

export const statusLabels: Record<number, string> = {
  1: 'Open',
  2: 'In Progress',
  3: 'Resolved',
  4: 'Closed',
};

export const incidentStatusOptions: IncidentStatusOption[] = [
  { value: 1, label: 'Open' },
  { value: 2, label: 'In Progress' },
  { value: 3, label: 'Resolved' },
  { value: 4, label: 'Closed' },
];

export interface Incident {
  id: string;
  title: string;
  description: string | null;
  severity: number;
  status: number;
  createdAt: string;
  createdByUserId?: string;
}

export interface CreateIncidentRequest {
  title: string;
  description: string | null;
  severity: number;
}

export interface UpdateIncidentStatusRequest {
  status: IncidentStatus;
}

export interface AuditEvent {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  occurredAt: string;
  actorId: string | null;
  actorEmail: string | null;
  metadataJson: string | null;
}

export async function listIncidents(token: string): Promise<Incident[]> {
  return apiRequest<Incident[]>('/incidents', {
    method: 'GET',
    token,
  });
}

export async function getIncidentById(id: string, token: string): Promise<Incident> {
  return apiRequest<Incident>(`/incidents/${id}`, {
    method: 'GET',
    token,
  });
}

export async function createIncident(
  request: CreateIncidentRequest,
  token: string,
): Promise<Incident> {
  return apiRequest<Incident>('/incidents', {
    method: 'POST',
    body: request,
    token,
  });
}

export async function updateIncidentStatus(
  id: string,
  status: IncidentStatus,
  token: string,
): Promise<Incident> {
  return apiRequest<Incident>(`/incidents/${id}/status`, {
    method: 'PATCH',
    body: { status } satisfies UpdateIncidentStatusRequest,
    token,
  });
}

export async function getIncidentAudit(id: string, token: string): Promise<AuditEvent[]> {
  return apiRequest<AuditEvent[]>(`/incidents/${id}/audit`, {
    method: 'GET',
    token,
  });
}
