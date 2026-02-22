import {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
  useCallback,
  type ReactNode,
} from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@/providers/auth-provider';
import { createNotificationConnection, type NotificationPayload } from '@/lib/signalr';
import type { HubConnection } from '@microsoft/signalr';

interface NotificationHubContextType {
  lastNotification: NotificationPayload | null;
  clearLastNotification: () => void;
}

const NotificationHubContext = createContext<NotificationHubContextType | null>(null);

export function NotificationHubProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [lastNotification, setLastNotification] = useState<NotificationPayload | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  const clearLastNotification = useCallback(() => setLastNotification(null), []);

  useEffect(() => {
    if (!user?.token) return;

    const connection = createNotificationConnection();
    connectionRef.current = connection;

    connection.on('Notification', (payload: NotificationPayload) => {
      setLastNotification(payload);
      queryClient.invalidateQueries({ queryKey: ['notifications', 'unread'] });
    });

    connection
      .start()
      .catch((err) => console.error('SignalR connection failed:', err));

    return () => {
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  }, [user?.token, queryClient]);

  return (
    <NotificationHubContext.Provider value={{ lastNotification, clearLastNotification }}>
      {children}
    </NotificationHubContext.Provider>
  );
}

export function useNotificationHub() {
  const ctx = useContext(NotificationHubContext);
  if (!ctx) {
    throw new Error('useNotificationHub must be used within NotificationHubProvider');
  }
  return ctx;
}
