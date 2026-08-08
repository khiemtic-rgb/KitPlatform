import type { PharmacyTenantConfig } from '../tenants/types';
import { getTenantByHost, getTenantBySlug } from '../tenants/registry';
import { xuanhoa } from '../tenants/xuanhoa';
import { mapPublicContentToTenant } from './content-mapper';

const DEFAULT_SLUG =
  (typeof import.meta.env.PUBLIC_STOREFRONT_TENANT === 'string' &&
    import.meta.env.PUBLIC_STOREFRONT_TENANT.trim()) ||
  'xuanhoa';

const API_BASE =
  (typeof import.meta.env.PUBLIC_API_BASE_URL === 'string' &&
    import.meta.env.PUBLIC_API_BASE_URL.trim().replace(/\/$/, '')) ||
  '';

/** Pilot static dual-run only — never reuse for other published subdomains. */
const PILOT_STATIC_SLUG = 'xuanhoa';

export class StorefrontNotFoundError extends Error {
  constructor(message = 'Storefront not found') {
    super(message);
    this.name = 'StorefrontNotFoundError';
  }
}

function hostCandidates(request: Request, resolvedUrl?: URL): string[] {
  const forwarded = request.headers.get('x-forwarded-host')?.split(',')[0]?.trim() ?? '';
  const original = request.headers.get('x-original-host')?.trim() ?? '';
  const hostHeader = request.headers.get('host')?.trim() ?? '';
  let urlHost = '';
  try {
    urlHost = new URL(request.url).host;
  } catch {
    // ignore
  }

  const forwardedRfc = request.headers.get('forwarded') ?? '';
  const forwardedHostMatch = forwardedRfc.match(/host="?([^";,\s]+)"?/i);
  const forwardedHost = forwardedHostMatch?.[1]?.trim() ?? '';

  const resolvedHost = resolvedUrl?.host ?? resolvedUrl?.hostname ?? '';

  return [forwarded, original, forwardedHost, hostHeader, urlHost, resolvedHost]
    .map((h) => h.toLowerCase().split(':')[0] ?? '')
    .filter(Boolean);
}

/**
 * Prefer the public custom hostname (*.novixa.vn) over pages.dev /
 * X-Forwarded-Host when Cloudflare or the adapter rewrites Host.
 */
export function resolveRequestHost(request: Request, resolvedUrl?: URL): string {
  const candidates = hostCandidates(request, resolvedUrl);
  const novixa = candidates.find((h) => h.endsWith('.novixa.vn') && h !== 'novixa.vn');
  if (novixa) return novixa;
  const nonPages = candidates.find((h) => !h.endsWith('.pages.dev'));
  return nonPages ?? candidates[0] ?? '';
}

function slugFromHost(host: string): string | undefined {
  const h = host.trim().toLowerCase().split(':')[0] ?? '';
  if (!h || h === 'localhost' || h === '127.0.0.1') return undefined;
  if (h.endsWith('.pages.dev')) return undefined;
  if (h === 'novixa.vn') return undefined;
  if (h.endsWith('.novixa.vn')) {
    const sub = h.slice(0, -'.novixa.vn'.length);
    if (sub && !sub.includes('.')) return sub;
  }
  if (h.endsWith('.localhost')) {
    const sub = h.slice(0, -'.localhost'.length);
    if (sub) return sub;
  }
  return undefined;
}

function previewTokenFromRequest(request: Request): string | undefined {
  try {
    const url = new URL(request.url);
    const token = url.searchParams.get('previewToken')?.trim();
    return token || undefined;
  } catch {
    return undefined;
  }
}

function pilotStatic(): PharmacyTenantConfig {
  return getTenantBySlug(PILOT_STATIC_SLUG) ?? xuanhoa;
}

async function fetchPublishedBySlug(slug: string): Promise<PharmacyTenantConfig | null> {
  if (!API_BASE) return null;
  try {
    const res = await fetch(`${API_BASE}/api/public/pharmacy-storefront?slug=${encodeURIComponent(slug)}`, {
      headers: { Accept: 'application/json' },
    });
    if (res.status === 404) return null;
    if (!res.ok) throw new Error(`Storefront API ${res.status}`);
    const data = (await res.json()) as {
      slug?: string;
      tenantCode?: string;
      tenantName?: string;
      content?: Record<string, unknown>;
    };
    return mapPublicContentToTenant({
      slug: String(data.slug ?? slug),
      tenantCode: String(data.tenantCode ?? ''),
      tenantName: String(data.tenantName ?? ''),
      content: data.content ?? {},
    });
  } catch (err) {
    console.error('[storefront] public API failed', err);
    return null;
  }
}

async function fetchPreviewByToken(token: string): Promise<PharmacyTenantConfig | null> {
  if (!API_BASE) return null;
  try {
    const res = await fetch(
      `${API_BASE}/api/public/pharmacy-storefront/preview?token=${encodeURIComponent(token)}`,
      { headers: { Accept: 'application/json' }, cache: 'no-store' },
    );
    if (res.status === 404 || res.status === 401) return null;
    if (!res.ok) throw new Error(`Storefront preview API ${res.status}`);
    const data = (await res.json()) as {
      slug?: string;
      tenantCode?: string;
      tenantName?: string;
      content?: Record<string, unknown>;
    };
    return mapPublicContentToTenant({
      slug: String(data.slug ?? 'preview'),
      tenantCode: String(data.tenantCode ?? ''),
      tenantName: String(data.tenantName ?? ''),
      content: data.content ?? {},
      isPreview: true,
    });
  } catch (err) {
    console.error('[storefront] preview API failed', err);
    return null;
  }
}

/**
 * Resolve white-label tenant from request Host (SSR).
 * Other slugs never fall back to Xuân Hòa static content.
 * Optional `?previewToken=` loads draft CMS content.
 *
 * Pass Astro.url as `resolvedUrl` — on Cloudflare it often has the public
 * hostname when request.url / Host have been rewritten to pages.dev.
 */
export async function resolveTenant(
  request: Request,
  resolvedUrl?: URL,
): Promise<PharmacyTenantConfig> {
  const previewToken = previewTokenFromRequest(request);
  if (previewToken) {
    const preview = await fetchPreviewByToken(previewToken);
    if (preview) return preview;
    throw new StorefrontNotFoundError('Preview token invalid or expired');
  }

  const candidates = hostCandidates(request, resolvedUrl);
  const host = resolveRequestHost(request, resolvedUrl);
  const hostSlug =
    slugFromHost(host) ??
    candidates.map(slugFromHost).find((s): s is string => Boolean(s));

  // Registry match (pilot hosts) — do not require CMS publish
  for (const candidate of candidates) {
    const byHost = getTenantByHost(candidate);
    if (byHost?.slug === PILOT_STATIC_SLUG) {
      const fromApi = await fetchPublishedBySlug(PILOT_STATIC_SLUG);
      return fromApi ?? byHost;
    }
  }

  if (hostSlug === PILOT_STATIC_SLUG || host.includes(PILOT_STATIC_SLUG)) {
    const fromApi = await fetchPublishedBySlug(PILOT_STATIC_SLUG);
    return fromApi ?? pilotStatic();
  }

  if (hostSlug) {
    const fromApi = await fetchPublishedBySlug(hostSlug);
    if (fromApi) return fromApi;
    throw new StorefrontNotFoundError(`No published storefront for ${hostSlug}`);
  }

  // Local / dev without subdomain
  if (import.meta.env.DEV) {
    const byHost = host ? getTenantByHost(host) : undefined;
    if (byHost) return byHost;

    if (API_BASE) {
      const fromApi = await fetchPublishedBySlug(DEFAULT_SLUG);
      if (fromApi) return fromApi;
    }

    const byEnv = getTenantBySlug(DEFAULT_SLUG);
    if (byEnv) return byEnv;

    return pilotStatic();
  }

  // Production pages.dev / Host not forwarded: keep dual-run pilot available
  // for the project default URL only. Custom `{slug}.novixa.vn` is handled above.
  const looksLikePagesDev =
    !host || host.endsWith('.pages.dev') || host === 'novixa.vn' || candidates.every((h) => h.endsWith('.pages.dev') || h === 'novixa.vn');
  if (looksLikePagesDev) {
    const fromApi = await fetchPublishedBySlug(DEFAULT_SLUG);
    return fromApi ?? pilotStatic();
  }

  throw new StorefrontNotFoundError(`No storefront hostname: ${host || '(empty)'}`);
}
