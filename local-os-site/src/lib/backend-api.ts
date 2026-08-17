/** Server-only. Browser must not call this — use /api/* on the same origin. */
export async function localOsApiBase(): Promise<string> {
  try {
    const mod = await import('cloudflare:workers');
    const env = (mod as { env?: { LOCAL_OS_API?: string; HITS?: unknown } }).env;
    const fromEnv = env?.LOCAL_OS_API?.replace(/\/$/, '');
    if (fromEnv) return fromEnv;
    if (env?.HITS) return 'https://api.novixa.vn/api/public/local-os';
  } catch {
    /* local Astro */
  }
  return (import.meta.env.PUBLIC_LOCAL_OS_API as string | undefined)?.replace(/\/$/, '')
    || 'http://127.0.0.1:5290/api/public/local-os';
}
