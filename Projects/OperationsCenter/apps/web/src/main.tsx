import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import { AuthProvider } from './auth/AuthContext';
import { OperationsRealtimeProvider } from './realtime/OperationsRealtimeProvider';
import './styles.css';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <AuthProvider>
      <OperationsRealtimeProvider>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </OperationsRealtimeProvider>
    </AuthProvider>
  </React.StrictMode>,
);
