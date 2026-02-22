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
  const wsUrl = config.webSocketUrl + (token ? `?token=${encodeURIComponent(token)}` : '');
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

export function createNotificationConnection(): NotificationConnection {
  if (config.webSocketUrl) {
    return createWebSocketConnection();
  }
  return createSignalRConnection();
}
