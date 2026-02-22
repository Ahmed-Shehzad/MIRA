import * as signalR from '@microsoft/signalr';
import { config } from '@/config/env';

const HUB_URL = `${config.apiUrl}/hubs/notifications`;

export function createNotificationConnection() {
  const token = localStorage.getItem('token');
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => token ?? '',
    })
    .withAutomaticReconnect()
    .build();
  return connection;
}

export type NotificationPayload = { type: string; title: string; body?: string };
