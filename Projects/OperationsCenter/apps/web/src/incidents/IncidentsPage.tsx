import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/apiClient';
import { type Incident, listIncidents, severityLabels, statusLabels } from '../api/incidentsApi';
import { useAuth } from '../auth/AuthContext';

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

export function IncidentsPage(): JSX.Element {
  const { accessToken, logout } = useAuth();
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isCancelled = false;

    const load = async (): Promise<void> => {
      if (!accessToken) {
        setIsLoading(false);
        setErrorMessage('Missing access token.');
        return;
      }

      setIsLoading(true);
      setErrorMessage(null);

      try {
        const response = await listIncidents(accessToken);
        if (!isCancelled) {
          setIncidents(response);
        }
      } catch (error) {
        if (isCancelled) {
          return;
        }

        if (error instanceof ApiError && error.kind === 'unauthorized') {
          logout();
          setErrorMessage('Your session has expired. Please sign in again.');
          return;
        }

        if (error instanceof ApiError && error.kind === 'forbidden') {
          setErrorMessage('You are not allowed to read incidents.');
          return;
        }

        setErrorMessage('Failed to load incidents.');
      } finally {
        if (!isCancelled) {
          setIsLoading(false);
        }
      }
    };

    void load();

    return () => {
      isCancelled = true;
    };
  }, [accessToken, logout]);

  if (isLoading) {
    return <p>Loading incidents...</p>;
  }

  if (errorMessage) {
    return (
      <div className="card">
        <p className="error">{errorMessage}</p>
      </div>
    );
  }

  if (incidents.length === 0) {
    return (
      <div className="card">
        <div className="page-actions">
          <h2>Incidents</h2>
          <Link to="/incidents/new" className="link-button">
            Create incident
          </Link>
        </div>
        <p className="muted">No incidents found.</p>
      </div>
    );
  }

  return (
    <div className="card">
      <div className="page-actions">
        <h2>Incidents</h2>
        <Link to="/incidents/new" className="link-button">
          Create incident
        </Link>
      </div>
      <table>
        <thead>
          <tr>
            <th>Title</th>
            <th>Severity</th>
            <th>Status</th>
            <th>Created At</th>
          </tr>
        </thead>
        <tbody>
          {incidents.map((incident) => (
            <tr key={incident.id}>
              <td>
                <Link to={`/incidents/${incident.id}`}>{incident.title}</Link>
              </td>
              <td>{severityLabels[incident.severity] ?? String(incident.severity)}</td>
              <td>{statusLabels[incident.status] ?? String(incident.status)}</td>
              <td>{formatDate(incident.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
