import { FormEvent, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../api/apiClient';
import { createIncident, severityLabels } from '../api/incidentsApi';
import { useAuth } from '../auth/AuthContext';

const severityValues = [1, 2, 3, 4] as const;

interface ValidationProblemLike {
  errors?: Record<string, string[]>;
}

function getBackendValidationMessage(error: ApiError): string | null {
  const value = error.payload;
  if (!value || typeof value !== 'object') {
    return null;
  }

  const payload = value as ValidationProblemLike;
  if (!payload.errors) {
    return null;
  }

  const messages = Object.values(payload.errors).flat();
  return messages.length > 0 ? messages[0] : null;
}

export function CreateIncidentPage(): JSX.Element {
  const { accessToken, logout } = useAuth();
  const navigate = useNavigate();

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [severity, setSeverity] = useState<number>(2);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const severityOptions = useMemo(
    () => severityValues.map((value) => ({ value, label: severityLabels[value] })),
    [],
  );

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setErrorMessage(null);

    if (!title.trim()) {
      setErrorMessage('Title is required.');
      return;
    }

    if (!accessToken) {
      setErrorMessage('Missing access token.');
      return;
    }

    setIsSubmitting(true);

    try {
      const created = await createIncident(
        {
          title: title.trim(),
          description: description.trim() ? description.trim() : null,
          severity,
        },
        accessToken,
      );

      navigate(`/incidents/${created.id}`, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.kind === 'unauthorized') {
        logout();
        setErrorMessage('Your session has expired. Please sign in again.');
      } else if (error instanceof ApiError && error.kind === 'forbidden') {
        setErrorMessage('You are not allowed to create incidents.');
      } else if (error instanceof ApiError && error.status === 400) {
        setErrorMessage(getBackendValidationMessage(error) ?? 'Please fix validation errors.');
      } else {
        setErrorMessage('Failed to create incident.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form className="card create-form" onSubmit={handleSubmit}>
      <div className="page-actions">
        <h2>Create incident</h2>
        <Link to="/incidents" className="link-button">
          Back to incidents
        </Link>
      </div>

      <label htmlFor="incident-title">Title</label>
      <input
        id="incident-title"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        required
      />

      <label htmlFor="incident-description">Description</label>
      <textarea
        id="incident-description"
        value={description}
        onChange={(event) => setDescription(event.target.value)}
        rows={4}
      />

      <label htmlFor="incident-severity">Severity</label>
      <select
        id="incident-severity"
        value={severity}
        onChange={(event) => setSeverity(Number(event.target.value))}
      >
        {severityOptions.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>

      {errorMessage ? <p className="error">{errorMessage}</p> : null}

      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Creating...' : 'Create incident'}
      </button>
    </form>
  );
}
