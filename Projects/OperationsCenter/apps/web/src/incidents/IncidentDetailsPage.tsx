import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../api/apiClient';
import {
  getIncidentAudit,
  getIncidentById,
  incidentStatusOptions,
  severityLabels,
  statusLabels,
  type AuditEvent,
  type Incident,
  type IncidentStatus,
  updateIncidentStatus,
} from '../api/incidentsApi';
import { useAuth } from '../auth/AuthContext';

interface AuditMetadata {
  oldStatus?: string;
  newStatus?: string;
}

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
  const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([]);
  const [isAuditLoading, setIsAuditLoading] = useState(true);
  const [auditErrorMessage, setAuditErrorMessage] = useState<string | null>(null);

  const loadIncidentAudit = async (
    incidentId: string,
    token: string,
    isCancelled: boolean,
  ): Promise<void> => {
    setIsAuditLoading(true);
    setAuditErrorMessage(null);

    try {
      const response = await getIncidentAudit(incidentId, token);
      if (!isCancelled) {
        const ordered = [...response].sort(
          (a, b) => new Date(a.occurredAt).getTime() - new Date(b.occurredAt).getTime(),
        );
        setAuditEvents(ordered);
      }
    } catch (error) {
      if (isCancelled) {
        return;
      }

      if (error instanceof ApiError && error.kind === 'unauthorized') {
        logout();
        setAuditErrorMessage('Your session has expired. Please sign in again.');
      } else if (error instanceof ApiError && error.kind === 'forbidden') {
        setAuditErrorMessage('You do not have permission to view incident audit history.');
      } else if (error instanceof ApiError && error.status === 404) {
        setAuditErrorMessage('Incident audit history was not found.');
      } else {
        setAuditErrorMessage('Failed to load incident audit history.');
      }
    } finally {
      if (!isCancelled) {
        setIsAuditLoading(false);
      }
    }
  };

  const parseAuditMetadata = (metadataJson: string | null): AuditMetadata | null => {
    if (!metadataJson) {
      return null;
    }

    try {
      const parsed = JSON.parse(metadataJson) as unknown;
      if (!parsed || typeof parsed !== 'object') {
        return null;
      }

      const data = parsed as Record<string, unknown>;
      return {
        oldStatus: typeof data.oldStatus === 'string' ? data.oldStatus : undefined,
        newStatus: typeof data.newStatus === 'string' ? data.newStatus : undefined,
      };
    } catch {
      return null;
    }
  };

  const formatAuditAction = (event: AuditEvent): string => {
    if (event.action === 'Created') {
      return 'Incident created';
    }

    if (event.action === 'StatusChanged') {
      const metadata = parseAuditMetadata(event.metadataJson);
      if (metadata?.oldStatus && metadata?.newStatus) {
        return `Status changed from ${metadata.oldStatus} to ${metadata.newStatus}`;
      }

      return 'Status changed';
    }

    return event.action;
  };

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

        await loadIncidentAudit(id, accessToken, isCancelled);
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
                await loadIncidentAudit(id, accessToken, false);
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

      <div className="audit-timeline-panel">
        <h3>Audit history</h3>

        {isAuditLoading ? <p className="muted">Loading audit history...</p> : null}

        {!isAuditLoading && auditErrorMessage ? <p className="error">{auditErrorMessage}</p> : null}

        {!isAuditLoading && !auditErrorMessage && auditEvents.length === 0 ? (
          <p className="muted">No audit events found.</p>
        ) : null}

        {!isAuditLoading && !auditErrorMessage && auditEvents.length > 0 ? (
          <ol className="audit-timeline-list">
            {auditEvents.map((event) => (
              <li key={event.id} className="audit-timeline-item">
                <p className="audit-action">{formatAuditAction(event)}</p>
                <p className="audit-meta">{formatDate(event.occurredAt)}</p>
                <p className="audit-meta">Actor ID: {event.actorId ?? 'Unknown actor'}</p>
              </li>
            ))}
          </ol>
        ) : null}
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
