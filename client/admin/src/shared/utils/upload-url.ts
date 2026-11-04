import { apiPath } from '@/shared/api/api-base';
import { adminResourceRequest, stripAccessToken } from '@/shared/utils/bearer-transport';

/** Resolve an upload URL onto the API origin. Never appends a bearer token. */
export function withUploadAuth(url: string | undefined | null): string | undefined {
  if (!url?.trim()) return undefined;
  const trimmed = stripAccessToken(url.trim());

  let absolute = trimmed;
  if (trimmed.startsWith('/uploads/')) {
    absolute = apiPath(trimmed);
  } else if (trimmed.startsWith('uploads/')) {
    absolute = apiPath(`/${trimmed}`);
  }

  return adminResourceRequest(absolute, null).url;
}

export function adminUploadFetchPlan(url: string, token: string | null) {
  const absolute = withUploadAuth(url) ?? stripAccessToken(url);
  return adminResourceRequest(absolute, token);
}
