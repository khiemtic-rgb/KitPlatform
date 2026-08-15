import type { APIRoute } from 'astro';
import { readHits, summarize } from '../../lib/hits';

export const prerender = false;

export const GET: APIRoute = async () => {
  const stats = summarize(await readHits());
  return new Response(JSON.stringify(stats), {
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'access-control-allow-origin': '*',
      'cache-control': 'no-store',
    },
  });
};
