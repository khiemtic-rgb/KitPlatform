/** Base URL API (không có /api). Dev: để trống → Vite proxy. Prod: https://api.example.com */
export function resolveApiOrigin(): string {
  return (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
}

export function apiPath(path: string): string {
  const normalized = path.startsWith('/') ? path : `/${path}`;
  const origin = resolveApiOrigin();
  return origin ? `${origin}${normalized}` : normalized;
}
