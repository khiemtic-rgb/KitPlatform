import { apiPath } from '@/shared/api/api-base';
import { customerUploadRequest, stripAccessToken } from '@/shared/utils/bearer-transport';

/** Resolve an upload URL onto the API origin. Never appends a bearer token. */
export function withCustomerUploadAuth(url: string | undefined | null): string | undefined {
  if (!url?.trim()) return undefined;
  const trimmed = stripAccessToken(url.trim());
  if (!trimmed.startsWith('/uploads/') && !trimmed.startsWith('uploads/')) return trimmed;
  const path = trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  return customerUploadRequest(apiPath(path), null).url;
}

export function customerUploadFetchPlan(url: string, token: string | null) {
  const absolute = withCustomerUploadAuth(url) ?? stripAccessToken(url);
  return customerUploadRequest(absolute, token);
}

export async function openCustomerUpload(url: string, token: string | null): Promise<void> {
  const plan = customerUploadFetchPlan(url, token);
  if (!plan.authenticated || !plan.headers) {
    window.open(plan.url, '_blank', 'noopener,noreferrer');
    return;
  }

  const response = await fetch(plan.url, { headers: plan.headers, credentials: 'same-origin' });
  if (!response.ok) throw new Error('upload');
  const blob = await response.blob();
  const objectUrl = URL.createObjectURL(blob);
  window.open(objectUrl, '_blank', 'noopener,noreferrer');
  window.addEventListener('pagehide', () => URL.revokeObjectURL(objectUrl), { once: true });
}
