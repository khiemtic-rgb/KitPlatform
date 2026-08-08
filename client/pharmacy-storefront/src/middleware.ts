import { defineMiddleware } from 'astro:middleware';
import { resolveRequestHost } from './lib/tenant';

/**
 * Host-specific HTML must never be shared across {slug}.novixa.vn at the CDN.
 * Without this, a mistaken resolve (or old bug) can stick as cached Xuân Hòa HTML.
 */
export const onRequest = defineMiddleware(async (context, next) => {
  const response = await next();
  const headers = new Headers(response.headers);
  headers.set('Cache-Control', 'private, no-store, max-age=0, must-revalidate');
  headers.set('CDN-Cache-Control', 'no-store');
  headers.set('Cloudflare-CDN-Cache-Control', 'no-store');
  headers.set('Vary', 'Host, Accept-Encoding');
  // Short-lived diagnostics for multi-tenant Host resolution on Pages.
  headers.set('X-Sf-Host', resolveRequestHost(context.request, context.url));
  headers.set('X-Sf-Url', context.url.hostname);
  headers.set('X-Sf-Req', (() => {
    try {
      return new URL(context.request.url).hostname;
    } catch {
      return '';
    }
  })());
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
});
