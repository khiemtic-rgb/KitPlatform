import { useSessionStore } from '@/shared/auth/session.store';

/** Resolve /uploads/... for <img src> (Bearer via query — img cannot set Authorization). */
export function withEvidenceAuth(url: string | undefined | null): string | undefined {
  if (!url?.trim()) return undefined;
  let trimmed = url.trim();

  // Local absolute evidence URLs break when Vite proxies /api+/uploads to remote.
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

  if (!absolute.includes('/uploads/family-os/')) return absolute;
  if (/[?&]access_token=/.test(absolute)) return absolute;

  const token = useSessionStore.getState().accessToken;
  if (!token) return absolute;
  const separator = absolute.includes('?') ? '&' : '?';
  return `${absolute}${separator}access_token=${encodeURIComponent(token)}`;
}
