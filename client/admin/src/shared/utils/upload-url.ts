import { useAuthStore } from '@/shared/auth/auth.store';

export function withUploadAuth(url: string | undefined | null): string | undefined {
  if (!url?.trim()) return undefined;
  const trimmed = url.trim();
  if (!trimmed.startsWith('/uploads/')) return trimmed;

  const token = useAuthStore.getState().accessToken;
  if (!token) return trimmed;

  const separator = trimmed.includes('?') ? '&' : '?';
  return `${trimmed}${separator}access_token=${encodeURIComponent(token)}`;
}
