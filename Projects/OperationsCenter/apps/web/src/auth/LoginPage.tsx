import { FormEvent, useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../api/apiClient';
import { useAuth } from './AuthContext';

export function LoginPage(): JSX.Element {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('admin@operations-center.local');
  const [password, setPassword] = useState('Admin123!');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (auth.isAuthenticated) {
    return <Navigate to="/incidents" replace />;
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      await auth.login({ email, password });
      const redirectPath = (location.state as { from?: string } | null)?.from ?? '/incidents';
      navigate(redirectPath, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.kind === 'unauthorized') {
        setErrorMessage('Invalid email or password.');
      } else if (error instanceof ApiError && error.kind === 'config') {
        setErrorMessage('Frontend API configuration is missing or invalid.');
      } else if (error instanceof ApiError && error.kind === 'forbidden') {
        setErrorMessage('This account is inactive or lacks access.');
      } else {
        setErrorMessage('Login failed. Please try again.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="page-center">
      <form className="card" onSubmit={handleSubmit}>
        <h2>Sign In</h2>
        <p className="muted">Use a seeded development user to access incidents.</p>

        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          required
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />

        {errorMessage ? <p className="error">{errorMessage}</p> : null}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}
