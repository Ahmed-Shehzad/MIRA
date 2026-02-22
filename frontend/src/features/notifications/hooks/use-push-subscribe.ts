import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replaceAll('-', '+').replaceAll('_', '/');
  const rawData = atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; i++) {
    outputArray[i] = rawData.codePointAt(i) ?? 0;
  }
  return outputArray;
}

async function subscribeToPush(vapidPublicKey: string): Promise<void> {
  if (!('serviceWorker' in navigator) || !('PushManager' in globalThis)) {
    throw new Error('Push notifications are not supported');
  }

  const permission = await Notification.requestPermission();
  if (permission !== 'granted') {
    throw new Error('Notification permission denied');
  }

  const registration = await navigator.serviceWorker.register('/sw.js');
  await navigator.serviceWorker.ready;

  const subscription = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(vapidPublicKey) as BufferSource,
  });

  const subscriptionJson = subscription.toJSON();
  await api.post('/notifications/push/subscribe', {
    endpoint: subscriptionJson.endpoint,
    keys: subscriptionJson.keys
      ? {
          p256dh: subscriptionJson.keys.p256dh,
          auth: subscriptionJson.keys.auth,
        }
      : undefined,
  });
}

export function useVapidPublicKey() {
  return useQuery({
    queryKey: ['notifications', 'vapid-public-key'],
    queryFn: async () => {
      const { data } = await api.get<{ vapidPublicKey: string | null }>('/notifications/push/vapid-public-key');
      return data.vapidPublicKey;
    },
  });
}

export function usePushSubscribe() {
  return useMutation({
    mutationFn: async (vapidPublicKey: string) => {
      await subscribeToPush(vapidPublicKey);
    },
  });
}
