import { apiRequest } from './apiClient';

export interface Incident {
  id: string;
  title: string;
  description: string | null;
  severity: number;
  status: number;
  createdAt: string;
}

export async function listIncidents(token: string): Promise<Incident[]> {
  return apiRequest<Incident[]>('/incidents', {
    method: 'GET',
    token,
  });
}
