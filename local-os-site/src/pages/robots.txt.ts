import type { APIRoute } from 'astro';

export const prerender = false;

const FALLBACK = 'https://thainguyenlife.vn';

export const GET: APIRoute = ({ site }) => {
  const origin = (site ?? new URL(FALLBACK)).origin.replace(/\/$/, '');
  const body = [
    'User-agent: *',
    'Allow: /',
    'Disallow: /api/',
    'Disallow: /thong-ke',
    '',
    `Sitemap: ${origin}/sitemap.xml`,
    '',
  ].join('\n');
  return new Response(body, {
    headers: {
      'Content-Type': 'text/plain; charset=utf-8',
      'Cache-Control': 'public, max-age=86400',
    },
  });
};
