import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/apiClient';
import { getIncidentById, severityLabels, statusLabels, type Incident } from '../api/incidentsApi';
import { useAuth } from '../auth/AuthContext';

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

export function IncidentDetailsPage(): JSX.Element {
  const { id } = useParams<{ id: string }>();
  const { accessToken, logout } = useAuth();

  const [incident, setIncident] = useState<Incident | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isCancelled = false;

    const load = async (): Promise<void> => {
      if (!id) {
        setErrorMessage('Incident id is missing.');
        setIsLoading(false);
        return;
      }

      if (!accessToken) {
        setErrorMessage('Missing access token.');
        setIsLoading(false);
        return;
      }

      setIsLoading(true);
      setErrorMessage(null);

      try {
        const response = await getIncidentById(id, accessToken);
        if (!isCancelled) {
          setIncident(response);
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
          setErrorMessage('You are not allowed to view this incident.');
          return;
        }

        if (error instanceof ApiError && error.status === 404) {
          setErrorMessage('Incident not found.');
          return;
        }

        setErrorMessage('Failed to load incident details.');
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
  }, [id, accessToken, logout]);

  if (isLoading) {
    return <p>Loading incident details...</p>;
  }

  if (errorMessage) {
    return (
      <div className="card">
        <p className="error">{errorMessage}</p>
        <div className="page-actions">
          <Link to="/incidents" className="link-button">
            Back to incidents
          </Link>
        </div>
      </div>
    );
  }

  if (!incident) {
    return (
      <div className="card">
        <p className="error">Incident was not loaded.</p>
        <div className="page-actions">
          <Link to="/incidents" className="link-button">
            Back to incidents
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="card">
      <div className="page-actions">
        <h2>Incident details</h2>
        <Link to="/incidents" className="link-button">
          Back to incidents
        </Link>
      </div>

      <dl className="details-grid">
        <dt>Title</dt>
        <dd>{incident.title}</dd>

        <dt>Description</dt>
        <dd>{incident.description || '-'}</dd>

        <dt>Severity</dt>
        <dd>{severityLabels[incident.severity] ?? String(incident.severity)}</dd>

        <dt>Status</dt>
        <dd>{statusLabels[incident.status] ?? String(incident.status)}</dd>

        <dt>Created At</dt>
        <dd>{formatDate(incident.createdAt)}</dd>

        {incident.createdByUserId ? (
          <>
            <dt>Created By User Id</dt>
            <dd>{incident.createdByUserId}</dd>
          </>
        ) : null}
      </dl>
    </div>
  );
}
