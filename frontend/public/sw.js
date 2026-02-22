/* eslint-env serviceworker */
self.addEventListener('push', (event) => {
  if (!event.data) return;
  let payload = { title: 'Notification', body: '' };
  try {
    payload = event.data.json();
  } catch {
    payload.body = event.data.text();
  }
  event.waitUntil(
    self.registration.showNotification(payload.title || 'HIVE Food Orders', {
      body: payload.body || '',
      icon: '/vite.svg',
      tag: payload.tag || 'hive-notification',
    })
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      if (clientList.length > 0) {
        clientList[0].focus();
      } else if (self.clients.openWindow) {
        self.clients.openWindow('/');
      }
    })
  );
});
