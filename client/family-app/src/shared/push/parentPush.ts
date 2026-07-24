import { http } from '@/shared/api/http';

function urlBase64ToUint8Array(base64String: string) {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) outputArray[i] = rawData.charCodeAt(i);
  return outputArray;
}

export function isParentPushSupported() {
  return (
    typeof window !== 'undefined' &&
    'serviceWorker' in navigator &&
    'PushManager' in window &&
    'Notification' in window
  );
}

export async function ensureFamilyServiceWorker(): Promise<ServiceWorkerRegistration> {
  const existing = await navigator.serviceWorker.getRegistration('/');
  if (existing) return existing;
  return navigator.serviceWorker.register('/sw.js', { scope: '/' });
}

export async function subscribeParentPush(publicKey: string) {
  const registration = await ensureFamilyServiceWorker();
  await navigator.serviceWorker.ready;
  const existing = await registration.pushManager.getSubscription();
  const subscription =
    existing ??
    (await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(publicKey),
    }));

  const json = subscription.toJSON();
  if (!json.endpoint || !json.keys?.p256dh || !json.keys.auth) {
    throw new Error('Không đọc được subscription push');
  }
  return {
    endpoint: json.endpoint,
    p256dh: json.keys.p256dh,
    auth: json.keys.auth,
  };
}

export async function fetchParentPushStatus(familyId: string, membershipId?: string) {
  const { data } = await http.get<Record<string, unknown>>(
    `/family-os/families/${familyId}/parent-push/status`,
    { params: membershipId ? { membershipId } : undefined },
  );
  return {
    supported: Boolean(data.supported ?? data.Supported),
    subscribed: Boolean(data.subscribed ?? data.Subscribed),
    publicKey:
      data.publicKey != null || data.PublicKey != null
        ? String(data.publicKey ?? data.PublicKey)
        : undefined,
  };
}

export async function registerParentPushSubscription(
  familyId: string,
  input: { membershipId: string; endpoint: string; p256dh: string; auth: string },
) {
  await http.post(`/family-os/families/${familyId}/parent-push/subscribe`, {
    membershipId: input.membershipId,
    endpoint: input.endpoint,
    p256dh: input.p256dh,
    auth: input.auth,
    userAgent: typeof navigator !== 'undefined' ? navigator.userAgent.slice(0, 240) : null,
  });
}
