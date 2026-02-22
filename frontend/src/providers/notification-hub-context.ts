import { createContext, useContext } from 'react';
import type { NotificationPayload } from '@/lib/notification-connection';

export interface NotificationHubContextType {
  lastNotification: NotificationPayload | null;
  clearLastNotification: () => void;
}

export const NotificationHubContext = createContext<NotificationHubContextType | null>(null);

export function useNotificationHub() {
  const ctx = useContext(NotificationHubContext);
  if (!ctx) {
    throw new Error('useNotificationHub must be used within NotificationHubProvider');
  }
  return ctx;
}
