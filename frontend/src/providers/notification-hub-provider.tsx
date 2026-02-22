import {
  createNotificationConnection,
  type NotificationConnection,
  type NotificationPayload,
} from "@/lib/notification-connection";
import { useAuth } from "@/providers/auth-provider";
import {
  NotificationHubContext,
  type NotificationHubContextType,
} from "@/providers/notification-hub-context";
import { useQueryClient } from "@tanstack/react-query";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";

export function NotificationHubProvider({
  children,
}: Readonly<{ children: ReactNode }>) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [lastNotification, setLastNotification] =
    useState<NotificationPayload | null>(null);
  const connectionRef = useRef<NotificationConnection | null>(null);

  const clearLastNotification = useCallback(
    () => setLastNotification(null),
    [],
  );

  useEffect(() => {
    if (!user?.token) return;

    const connection = createNotificationConnection();
    connectionRef.current = connection;

    connection.on("Notification", (payload: NotificationPayload) => {
      setLastNotification(payload);
      queryClient.invalidateQueries({ queryKey: ["notifications", "unread"] });
    });

    connection
      .start()
      .catch((err) => console.error("SignalR connection failed:", err));

    return () => {
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  }, [user?.token, queryClient]);

  const value = useMemo<NotificationHubContextType>(
    () => ({ lastNotification, clearLastNotification }),
    [lastNotification, clearLastNotification],
  );

  return (
    <NotificationHubContext.Provider value={value}>
      {children}
    </NotificationHubContext.Provider>
  );
}
