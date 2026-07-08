import { Navigate, Route, Routes } from 'react-router-dom';
import { LoginPage } from './auth/LoginPage';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { useAuth } from './auth/AuthContext';
import { CreateIncidentPage } from './incidents/CreateIncidentPage';
import { IncidentDetailsPage } from './incidents/IncidentDetailsPage';
import { IncidentsPage } from './incidents/IncidentsPage';
import { AppLayout } from './layout/AppLayout';

function RootRoute(): JSX.Element {
  const auth = useAuth();
  return <Navigate to={auth.isAuthenticated ? '/incidents' : '/login'} replace />;
}

export default function App(): JSX.Element {
  return (
    <Routes>
      <Route path="/" element={<RootRoute />} />
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/incidents"
        element={
          <ProtectedRoute>
            <AppLayout>
              <IncidentsPage />
            </AppLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/incidents/new"
        element={
          <ProtectedRoute>
            <AppLayout>
              <CreateIncidentPage />
            </AppLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/incidents/:id"
        element={
          <ProtectedRoute>
            <AppLayout>
              <IncidentDetailsPage />
            </AppLayout>
          </ProtectedRoute>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
