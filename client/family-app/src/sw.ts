/* FamilyOS: precache SPA + parent Web Push — autoUpdate qua vite-plugin-pwa. */
/// <reference lib="webworker" />
import { clientsClaim } from 'workbox-core';
import { precacheAndRoute } from 'workbox-precaching';

declare let self: ServiceWorkerGlobalScope & {
  __WB_MANIFEST: Array<{ url: string; revision: string | null }>;
};

clientsClaim();
precacheAndRoute(self.__WB_MANIFEST);

self.addEventListener('install', () => {
  void self.skipWaiting();
});

self.addEventListener('push', (event) => {
  let payload: {
    title?: string;
    body?: string;
    silent?: boolean;
    tag?: string;
    data?: { url?: string; type?: string };
  } = {
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
  const silent = payload.silent === true;
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
    } as NotificationOptions),
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const url =
    (event.notification.data as { url?: string } | undefined)?.url || '/today';
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      for (const client of clients) {
        if ('focus' in client) {
          const focused = client as WindowClient;
          if ('navigate' in focused && typeof focused.navigate === 'function') {
            void focused.navigate(url);
          }
          return focused.focus();
        }
      }
      if (self.clients.openWindow) return self.clients.openWindow(url);
    }),
  );
});

export {};
