/* FamilyOS parent Web Push service worker */
self.addEventListener('push', (event) => {
  let payload = {
    title: 'FamilyOS',
    body: '',
    silent: false,
    data: { url: '/today' },
  };
  try {
    if (event.data) payload = { ...payload, ...event.data.json() };
  } catch {
    /* ignore */
  }
  const title = payload.title || 'FamilyOS';
  const body = payload.body || '';
  const url = payload.data?.url || '/today';
  const type = payload.data?.type || 'familyos_parent_reminder';
  const silent = payload.silent === true; // default: system sound ON
  const tag =
    payload.tag ||
    (type === 'familyos_gratitude'
      ? 'familyos-gratitude'
      : type === 'familyos_surprise'
        ? 'familyos-surprise'
        : type === 'familyos_approval_digest'
          ? 'familyos-approval'
          : type === 'familyos_child_request'
            ? 'familyos-child-request'
            : type === 'familyos_ai_proposal'
              ? 'familyos-ai-proposal'
              : 'familyos-parent');
  event.waitUntil(
    self.registration.showNotification(title, {
      body,
      data: { url, type },
      tag,
      silent,
      vibrate: silent ? undefined : [180, 80, 180],
      renotify: true,
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
