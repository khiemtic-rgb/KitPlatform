import type { APIRoute } from 'astro';
import { feedToken, writeLiveFeed, type PublicFeed } from '../../lib/live-feed';

export const prerender = false;

function unauthorized() {
  return new Response(JSON.stringify({ message: 'Unauthorized' }), {
    status: 401,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
}

export const POST: APIRoute = async ({ request }) => {
  const expected = await feedToken();
  if (!expected) return unauthorized();
  const got = (request.headers.get('authorization') ?? '').replace(/^Bearer\s+/i, '').trim();
  if (got !== expected) return unauthorized();

  let body: PublicFeed;
  try {
    body = (await request.json()) as PublicFeed;
  } catch {
    return new Response(JSON.stringify({ message: 'JSON không hợp lệ.' }), {
      status: 400,
      headers: { 'content-type': 'application/json; charset=utf-8' },
    });
  }

  try {
    const count = await writeLiveFeed(body);
    return new Response(JSON.stringify({ ok: true, listingCount: count }), {
      headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Không ghi được feed.';
    return new Response(JSON.stringify({ message }), {
      status: 400,
      headers: { 'content-type': 'application/json; charset=utf-8' },
    });
  }
};
