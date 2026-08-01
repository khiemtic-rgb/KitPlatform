/// <reference lib="webworker" />
import { clientsClaim } from 'workbox-core';
import { precacheAndRoute } from 'workbox-precaching';

declare let self: ServiceWorkerGlobalScope & {
  __WB_MANIFEST: Array<{ url: string; revision: string | null } | string>;
};

clientsClaim();

/** Bỏ lazy page/icon chunks khỏi precache — lần mở đầu không tranh bandwidth với Home. */
function shouldPrecache(url: string): boolean {
  if (!url.includes('/assets/')) return true;
  if (/(Page|Inbox|Panel)-[^/]+\.js$/i.test(url)) return false;
  if (/(Outlined|Filled|TwoTone)-[^/]+\.js$/i.test(url)) return false;
  return true;
}

const manifest = (self.__WB_MANIFEST ?? []).filter((entry) => {
  const url = typeof entry === 'string' ? entry : entry.url;
  return shouldPrecache(url);
});

precacheAndRoute(manifest);

self.addEventListener('install', () => {
  void self.skipWaiting();
});

self.addEventListener('push', (event: PushEvent) => {
  event.waitUntil(
    (async () => {
      let title = 'KitPlatform';
      let body = 'You have a new notification';
      let data: unknown;

      try {
        const payload = event.data?.json() as { title?: string; body?: string; data?: unknown } | undefined;
        if (payload?.title) title = payload.title;
        if (payload?.body) body = payload.body;
        data = payload?.data ?? payload;
      } catch {
        const text = event.data?.text();
        if (text) body = text;
      }

      await self.registration.showNotification(title, {
        body,
        icon: '/icon-512.png',
        badge: '/icon-512.png',
        data,
      });
    })(),
  );
});

function resolveNotificationUrl(data: unknown): string {
  const payload = data as { type?: string; url?: string } | undefined;
  const url = typeof payload?.url === 'string' ? payload.url.trim() : '';
  if (url.startsWith('/')) return url;

  if (payload?.type === 'staff_chat_reply') return '/chat';
  if (payload?.type === 'customer_draft_order') return '/orders';
  if (payload?.type === 'repurchase_due') return '/reminders?tab=repurchase';
  return '/reminders';
}

self.addEventListener('notificationclick', (event: NotificationEvent) => {
  event.notification.close();
  event.waitUntil(
    (async () => {
      const targetUrl = resolveNotificationUrl(event.notification.data);

      const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
      for (const client of clients) {
        if ('focus' in client) {
          await client.focus();
          if ('navigate' in client && typeof client.navigate === 'function') {
            await client.navigate(targetUrl);
          }
          return;
        }
      }
      await self.clients.openWindow(targetUrl);
    })(),
  );
});

export {};
