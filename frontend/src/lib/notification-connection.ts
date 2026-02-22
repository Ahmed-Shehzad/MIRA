import { config } from '@/config/env';
import { api } from '@/lib/api';

export type NotificationPayload = { type: string; title: string; body?: string };

export interface NotificationConnection {
  on(event: string, callback: (payload: NotificationPayload) => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

function handleWebSocketMessage(
  data: string,
  handlers: ((payload: NotificationPayload) => void)[],
): void {
  try {
    const payload = JSON.parse(data) as NotificationPayload;
    handlers.forEach((h) => h(payload));
  } catch {
    // ignore non-JSON
  }
}

function connectWebSocket(
  wsUrl: string,
  handlers: ((payload: NotificationPayload) => void)[],
): Promise<WebSocket> {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(wsUrl);
    ws.onopen = () => resolve(ws);
    ws.onerror = () => reject(new Error('WebSocket connection failed'));
    ws.onmessage = (e) => handleWebSocketMessage(e.data, handlers);
  });
}

function createWebSocketConnection(): NotificationConnection {
  const token = localStorage.getItem('token');
  const wsUrl =
    config.webSocketUrl + (token ? `?token=${encodeURIComponent(token)}` : '');
  let ws: WebSocket | null = null;

  const handlers: ((payload: NotificationPayload) => void)[] = [];

  return {
    on(_event: string, callback: (payload: NotificationPayload) => void) {
      handlers.push(callback);
    },
    async start() {
      ws = await connectWebSocket(wsUrl, handlers);
    },
    async stop() {
      ws?.close();
      ws = null;
    },
  };
}

function createPollingConnection(): NotificationConnection {
  const POLL_INTERVAL_MS = 30_000;
  let intervalId: ReturnType<typeof setInterval> | null = null;
  const handlers: ((payload: NotificationPayload) => void)[] = [];
  const seenIds = new Set<number>();

  return {
    on(_event: string, callback: (payload: NotificationPayload) => void) {
      handlers.push(callback);
    },
    async start() {
      const poll = async () => {
        try {
          const { data } = await api.get<{ id: number; type: string; title: string; body?: string }[]>(
            '/notifications/unread',
          );
          for (const n of data) {
            if (!seenIds.has(n.id)) {
              seenIds.add(n.id);
              const payload: NotificationPayload = { type: n.type, title: n.title, body: n.body };
              handlers.forEach((h) => h(payload));
            }
          }
        } catch {
          // ignore poll errors
        }
      };
      await poll();
      intervalId = setInterval(poll, POLL_INTERVAL_MS);
    },
    async stop() {
      if (intervalId) {
        clearInterval(intervalId);
        intervalId = null;
      }
      seenIds.clear();
    },
  };
}

export function createNotificationConnection(): NotificationConnection {
  if (config.webSocketUrl) {
    return createWebSocketConnection();
  }
  return createPollingConnection();
}
