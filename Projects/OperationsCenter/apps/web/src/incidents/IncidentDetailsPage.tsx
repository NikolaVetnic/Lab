import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/apiClient';
import {
  getIncidentById,
  incidentStatusOptions,
  severityLabels,
  statusLabels,
  type Incident,
  type IncidentStatus,
  updateIncidentStatus,
} from '../api/incidentsApi';
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
  const [selectedStatus, setSelectedStatus] = useState<IncidentStatus>(1);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [statusErrorMessage, setStatusErrorMessage] = useState<string | null>(null);
  const [statusSuccessMessage, setStatusSuccessMessage] = useState<string | null>(null);

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
          setSelectedStatus(response.status as IncidentStatus);
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

      <div className="status-update-panel">
        <h3>Update status</h3>

        <div className="status-update-controls">
          <select
            aria-label="Incident status"
            value={selectedStatus}
            onChange={(event) => {
              setSelectedStatus(Number(event.target.value) as IncidentStatus);
              setStatusErrorMessage(null);
              setStatusSuccessMessage(null);
            }}
            disabled={isUpdatingStatus}
          >
            {incidentStatusOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>

          <button
            type="button"
            disabled={isUpdatingStatus || selectedStatus === (incident.status as IncidentStatus)}
            onClick={async () => {
              if (!id || !accessToken || isUpdatingStatus) {
                return;
              }

              setIsUpdatingStatus(true);
              setStatusErrorMessage(null);
              setStatusSuccessMessage(null);

              try {
                const updated = await updateIncidentStatus(id, selectedStatus, accessToken);
                setIncident(updated);
                setSelectedStatus(updated.status as IncidentStatus);
                setStatusSuccessMessage('Incident status updated.');
              } catch (error) {
                if (error instanceof ApiError && error.kind === 'unauthorized') {
                  logout();
                  setStatusErrorMessage('Your session has expired. Please sign in again.');
                } else if (error instanceof ApiError && error.kind === 'forbidden') {
                  setStatusErrorMessage('You do not have permission to update this incident.');
                } else if (error instanceof ApiError && error.status === 409) {
                  setStatusErrorMessage('Invalid status transition for this incident.');
                } else if (error instanceof ApiError && error.status === 404) {
                  setStatusErrorMessage('Incident not found.');
                } else if (error instanceof ApiError && error.status === 400) {
                  setStatusErrorMessage(error.message || 'Invalid status value.');
                } else {
                  setStatusErrorMessage('Failed to update incident status.');
                }
              } finally {
                setIsUpdatingStatus(false);
              }
            }}
          >
            {isUpdatingStatus ? 'Saving...' : 'Save status'}
          </button>
        </div>

        {statusErrorMessage ? <p className="error">{statusErrorMessage}</p> : null}
        {statusSuccessMessage ? <p className="success">{statusSuccessMessage}</p> : null}
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
