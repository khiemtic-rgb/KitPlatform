/** Server-only admin API (KIT_LOCAL). Browser uses /api/review/*. */
export async function localOsAdminApiBase(): Promise<string> {
  try {
    const mod = await import('cloudflare:workers');
    const env = (mod as { env?: { LOCAL_OS_ADMIN_API?: string; HITS?: unknown } }).env;
    const fromEnv = env?.LOCAL_OS_ADMIN_API?.replace(/\/$/, '');
    if (fromEnv) return fromEnv;
    if (env?.HITS) return 'https://api.novixa.vn/api';
  } catch {
    /* local Astro */
  }
  return (import.meta.env.PUBLIC_LOCAL_OS_ADMIN_API as string | undefined)?.replace(/\/$/, '')
    || 'http://127.0.0.1:5290/api';
}

export function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
}

export function bearer(request: Request): string {
  return (request.headers.get('authorization') ?? '').replace(/^Bearer\s+/i, '').trim();
}
