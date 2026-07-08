import type { PropsWithChildren } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function AppLayout({ children }: PropsWithChildren): JSX.Element {
  const { logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = (): void => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="app-shell">
      <header className="topbar">
        <h1>Operations Center</h1>
        <button type="button" onClick={handleLogout}>
          Logout
        </button>
      </header>
      <main>{children}</main>
    </div>
  );
}
