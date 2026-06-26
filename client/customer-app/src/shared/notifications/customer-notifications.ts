export type CustomerNotificationKind = 'draft_order' | 'system';

export type CustomerNotification = {
  id: string;
  kind: CustomerNotificationKind;
  title: string;
  body: string;
  href?: string;
  createdAt: string;
  read: boolean;
};

const STORAGE_KEY = 'pharmacore.customer.notifications';
const MAX_ITEMS = 50;

function loadRaw(): CustomerNotification[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as CustomerNotification[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function saveRaw(items: CustomerNotification[]) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(items.slice(0, MAX_ITEMS)));
}

export function listCustomerNotifications(): CustomerNotification[] {
  return loadRaw().sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export function unreadCustomerNotificationCount(): number {
  return loadRaw().filter((item) => !item.read).length;
}

export function addCustomerNotification(input: {
  kind: CustomerNotificationKind;
  title: string;
  body: string;
  href?: string;
  dedupeKey?: string;
}): CustomerNotification {
  const items = loadRaw();
  if (input.dedupeKey) {
    const existing = items.find((item) => item.id === input.dedupeKey);
    if (existing) return existing;
  }

  const notification: CustomerNotification = {
    id: input.dedupeKey ?? crypto.randomUUID(),
    kind: input.kind,
    title: input.title,
    body: input.body,
    href: input.href,
    createdAt: new Date().toISOString(),
    read: false,
  };

  saveRaw([notification, ...items]);
  window.dispatchEvent(new CustomEvent('customer-notifications-changed'));
  return notification;
}

export function markCustomerNotificationRead(id: string) {
  const items = loadRaw();
  const next = items.map((item) => (item.id === id ? { ...item, read: true } : item));
  saveRaw(next);
  window.dispatchEvent(new CustomEvent('customer-notifications-changed'));
}

export function markAllCustomerNotificationsRead() {
  const items = loadRaw().map((item) => ({ ...item, read: true }));
  saveRaw(items);
  window.dispatchEvent(new CustomEvent('customer-notifications-changed'));
}

export function subscribeCustomerNotifications(listener: () => void): () => void {
  const handler = () => listener();
  window.addEventListener('customer-notifications-changed', handler);
  return () => window.removeEventListener('customer-notifications-changed', handler);
}
