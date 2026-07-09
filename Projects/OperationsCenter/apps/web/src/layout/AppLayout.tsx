import type { PropsWithChildren } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useOperationsRealtime } from '../realtime/useOperationsRealtime';

export function AppLayout({ children }: PropsWithChildren): JSX.Element {
  const { logout } = useAuth();
  const { connectionStatus } = useOperationsRealtime();
  const navigate = useNavigate();

  const handleLogout = (): void => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="app-shell">
      <header className="topbar">
        <h1>Operations Center</h1>
        <div className="topbar-actions">
          <span className={`live-status live-status-${connectionStatus}`}>
            {connectionStatus === 'connected'
              ? 'Live updates connected'
              : connectionStatus === 'reconnecting'
                ? 'Reconnecting live updates...'
                : 'Live updates unavailable'}
          </span>
          <button type="button" onClick={handleLogout}>
            Logout
          </button>
        </div>
      </header>
      <main>{children}</main>
    </div>
  );
}
