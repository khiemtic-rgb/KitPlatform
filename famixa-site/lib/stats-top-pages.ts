/** Bỏ qua request kỹ thuật / nội bộ khi hiển thị top trang. */
const PREFIX_SKIP = ['/cdn-cgi/', '/api/', '/_next/'];

const EXACT_SKIP = new Set(['/stats-snapshot.json', '/robots.txt', '/favicon.ico', '/favicon-32.png']);

const STATIC_EXT = /\.(png|jpe?g|svg|ico|css|js|mjs|woff2?|webp|json|xml|txt|map)$/i;

export function isPublicTopPagePath(path: string): boolean {
  if (!path) return false;
  const p = path.split('?')[0] ?? path;
  if (p === '/vi/thong-ke' || p === '/vi/thong-ke/') return false;
  if (EXACT_SKIP.has(p)) return false;
  if (PREFIX_SKIP.some((prefix) => p.startsWith(prefix))) return false;
  if (STATIC_EXT.test(p)) return false;
  return true;
}

export function filterTopPages<T extends { path: string }>(rows: T[]): T[] {
  return rows.filter((row) => isPublicTopPagePath(row.path));
}
