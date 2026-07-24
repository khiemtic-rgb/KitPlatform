/* FamilyOS parent Web Push service worker */
self.addEventListener('push', (event) => {
  let payload = { title: 'FamilyOS', body: '', data: { url: '/today' } };
  try {
    if (event.data) payload = { ...payload, ...event.data.json() };
  } catch {
    /* ignore */
  }
  const title = payload.title || 'FamilyOS';
  const body = payload.body || '';
  const url = payload.data?.url || '/today';
  event.waitUntil(
    self.registration.showNotification(title, {
      body,
      data: { url },
      tag: 'familyos-parent',
    }),
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const url = event.notification.data?.url || '/today';
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      for (const client of clients) {
        if ('focus' in client) {
          client.navigate?.(url);
          return client.focus();
        }
      }
      if (self.clients.openWindow) return self.clients.openWindow(url);
    }),
  );
});
