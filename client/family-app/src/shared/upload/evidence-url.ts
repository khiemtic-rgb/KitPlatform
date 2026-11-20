import { familyEvidenceRequest, stripAccessToken } from '@/shared/upload/bearer-transport';

/** Resolve /uploads/family-os for display. Never appends a bearer token. */
export function withEvidenceAuth(url: string | undefined | null): string | undefined {
  if (!url?.trim()) return undefined;
  let trimmed = stripAccessToken(url.trim());

  try {
    if (/^https?:\/\//i.test(trimmed)) {
      const parsed = new URL(trimmed);
      if (
        parsed.hostname === 'localhost' ||
        parsed.hostname === '127.0.0.1' ||
        parsed.hostname === '::1'
      ) {
        trimmed = `${parsed.pathname}${parsed.search}`;
      }
    }
  } catch {
    /* keep trimmed */
  }

  const absolute = trimmed.startsWith('http')
    ? trimmed
    : trimmed.startsWith('/')
      ? trimmed
      : `/${trimmed}`;

  return familyEvidenceRequest(absolute, null).url;
}

export function familyEvidenceFetchPlan(url: string, token: string | null) {
  const absolute = withEvidenceAuth(url) ?? stripAccessToken(url);
  return familyEvidenceRequest(absolute, token);
}
