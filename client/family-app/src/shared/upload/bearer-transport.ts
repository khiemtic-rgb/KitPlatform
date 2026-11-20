export function stripAccessToken(url: string): string {
  const hashIndex = url.indexOf('#');
  const hash = hashIndex >= 0 ? url.slice(hashIndex) : '';
  const withoutHash = hashIndex >= 0 ? url.slice(0, hashIndex) : url;
  const queryIndex = withoutHash.indexOf('?');
  if (queryIndex < 0) return url;
  const path = withoutHash.slice(0, queryIndex);
  const params = new URLSearchParams(withoutHash.slice(queryIndex + 1));
  params.delete('access_token');
  const rest = params.toString();
  return `${path}${rest ? `?${rest}` : ''}${hash}`;
}

export function familyEvidenceRequest(absoluteUrl: string, token: string | null): {
  url: string;
  authenticated: boolean;
  headers?: { Authorization: string };
} {
  const url = stripAccessToken(absoluteUrl);
  const authenticated = url.includes('/uploads/family-os/');
  if (!authenticated || !token) return { url, authenticated: false };
  return { url, authenticated: true, headers: { Authorization: `Bearer ${token}` } };
}
