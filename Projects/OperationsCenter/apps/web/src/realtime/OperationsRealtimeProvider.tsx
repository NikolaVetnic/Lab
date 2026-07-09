import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type PropsWithChildren,
} from 'react';
import type { HubConnection } from '@microsoft/signalr';
import { useAuth } from '../auth/AuthContext';
import {
  createOperationsHubConnection,
  incidentCreatedEventName,
  incidentStatusChangedEventName,
  type IncidentCreatedRealtimeMessage,
  type IncidentStatusChangedRealtimeMessage,
  type LiveConnectionStatus,
} from './operationsHub';

type IncidentCreatedListener = (message: IncidentCreatedRealtimeMessage) => void;
type IncidentStatusChangedListener = (message: IncidentStatusChangedRealtimeMessage) => void;

interface OperationsRealtimeContextValue {
  connectionStatus: LiveConnectionStatus;
  subscribeIncidentCreated: (listener: IncidentCreatedListener) => () => void;
  subscribeIncidentStatusChanged: (listener: IncidentStatusChangedListener) => () => void;
}

const OperationsRealtimeContext = createContext<OperationsRealtimeContextValue | null>(null);

export function OperationsRealtimeProvider({ children }: PropsWithChildren): JSX.Element {
  const { accessToken, isAuthenticated } = useAuth();
  const [connectionStatus, setConnectionStatus] = useState<LiveConnectionStatus>('disconnected');

  const incidentCreatedListenersRef = useRef(new Set<IncidentCreatedListener>());
  const incidentStatusChangedListenersRef = useRef(new Set<IncidentStatusChangedListener>());

  const subscribeIncidentCreated = useCallback((listener: IncidentCreatedListener) => {
    incidentCreatedListenersRef.current.add(listener);
    return () => {
      incidentCreatedListenersRef.current.delete(listener);
    };
  }, []);

  const subscribeIncidentStatusChanged = useCallback((listener: IncidentStatusChangedListener) => {
    incidentStatusChangedListenersRef.current.add(listener);
    return () => {
      incidentStatusChangedListenersRef.current.delete(listener);
    };
  }, []);

  useEffect(() => {
    let isCancelled = false;
    let connection: HubConnection | null = null;

    const stopConnection = async (): Promise<void> => {
      if (connection) {
        await connection.stop();
      }
    };

    const startConnection = async (): Promise<void> => {
      if (!isAuthenticated || !accessToken) {
        setConnectionStatus('disconnected');
        return;
      }

      connection = createOperationsHubConnection(() => accessToken);

      connection.on(incidentCreatedEventName, (message: IncidentCreatedRealtimeMessage) => {
        incidentCreatedListenersRef.current.forEach((listener) => listener(message));
      });

      connection.on(
        incidentStatusChangedEventName,
        (message: IncidentStatusChangedRealtimeMessage) => {
          incidentStatusChangedListenersRef.current.forEach((listener) => listener(message));
        },
      );

      connection.onreconnecting(() => {
        setConnectionStatus('reconnecting');
      });

      connection.onreconnected(() => {
        setConnectionStatus('connected');
      });

      connection.onclose(() => {
        setConnectionStatus('disconnected');
      });

      try {
        await connection.start();
        if (!isCancelled) {
          setConnectionStatus('connected');
        }
      } catch {
        if (!isCancelled) {
          setConnectionStatus('disconnected');
        }
      }
    };

    void startConnection();

    return () => {
      isCancelled = true;
      void stopConnection();
    };
  }, [accessToken, isAuthenticated]);

  const value = useMemo<OperationsRealtimeContextValue>(
    () => ({
      connectionStatus,
      subscribeIncidentCreated,
      subscribeIncidentStatusChanged,
    }),
    [connectionStatus, subscribeIncidentCreated, subscribeIncidentStatusChanged],
  );

  return (
    <OperationsRealtimeContext.Provider value={value}>
      {children}
    </OperationsRealtimeContext.Provider>
  );
}

export function useOperationsRealtimeContext(): OperationsRealtimeContextValue {
  const context = useContext(OperationsRealtimeContext);
  if (!context) {
    throw new Error('useOperationsRealtimeContext must be used within OperationsRealtimeProvider.');
  }

  return context;
}
