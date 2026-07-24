import { useSessionStore } from '@/shared/auth/session.store';

/** Resolve /uploads/... for <img src> (Bearer via query — img cannot set Authorization). */
export function withEvidenceAuth(url: string | undefined | null): string | undefined {
  if (!url?.trim()) return undefined;
  const trimmed = url.trim();
  const absolute = trimmed.startsWith('http')
    ? trimmed
    : trimmed.startsWith('/')
      ? trimmed
      : `/${trimmed}`;

  if (!absolute.includes('/uploads/family-os/')) return absolute;

  const token = useSessionStore.getState().accessToken;
  if (!token) return absolute;
  const separator = absolute.includes('?') ? '&' : '?';
  return `${absolute}${separator}access_token=${encodeURIComponent(token)}`;
}
