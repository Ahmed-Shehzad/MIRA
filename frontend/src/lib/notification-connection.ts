import * as signalR from '@microsoft/signalr';
import { config } from '@/config/env';

export type NotificationPayload = { type: string; title: string; body?: string };

export interface NotificationConnection {
  on(event: string, callback: (payload: NotificationPayload) => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

function createSignalRConnection(): NotificationConnection {
  const token = localStorage.getItem('token');
  const hubUrl = `${config.apiUrl}/hubs/notifications`;
  const hub = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => token ?? '' })
    .withAutomaticReconnect()
    .build();

  return {
    on(event: string, callback: (payload: NotificationPayload) => void) {
      hub.on(event, callback);
    },
    start: () => hub.start(),
    stop: () => hub.stop(),
  };
}

function createWebSocketConnection(): NotificationConnection {
  const token = localStorage.getItem('token');
  const wsUrl = config.webSocketUrl + (token ? `?token=${encodeURIComponent(token)}` : '');
  let ws: WebSocket | null = null;

  const handlers: ((payload: NotificationPayload) => void)[] = [];

  return {
    on(_event: string, callback: (payload: NotificationPayload) => void) {
      handlers.push(callback);
    },
    async start() {
      return new Promise<void>((resolve, reject) => {
        ws = new WebSocket(wsUrl);
        ws.onopen = () => resolve();
        ws.onerror = () => reject(new Error('WebSocket connection failed'));
        ws.onmessage = (e) => {
          try {
            const payload = JSON.parse(e.data) as NotificationPayload;
            handlers.forEach((h) => h(payload));
          } catch {
            // ignore non-JSON
          }
        };
      });
    },
    async stop() {
      ws?.close();
      ws = null;
    },
  };
}

export function createNotificationConnection(): NotificationConnection {
  if (config.webSocketUrl) {
    return createWebSocketConnection();
  }
  return createSignalRConnection();
}
