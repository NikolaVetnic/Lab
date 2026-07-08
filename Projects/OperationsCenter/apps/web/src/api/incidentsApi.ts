import { apiRequest } from './apiClient';

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
