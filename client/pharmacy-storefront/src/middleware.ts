import { defineMiddleware } from 'astro:middleware';

/**
 * Host-specific HTML must never be shared across {slug}.novixa.vn at the CDN.
 * Without this, a mistaken resolve (or old bug) can stick as cached Xuân Hòa HTML.
 */
export const onRequest = defineMiddleware(async (_context, next) => {
  const response = await next();
  const headers = new Headers(response.headers);
  headers.set('Cache-Control', 'private, no-store, max-age=0, must-revalidate');
  headers.set('CDN-Cache-Control', 'no-store');
  headers.set('Cloudflare-CDN-Cache-Control', 'no-store');
  headers.set('Vary', 'Host, Accept-Encoding');
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
});
