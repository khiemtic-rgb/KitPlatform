import type { APIRoute } from 'astro';
import { feedToken, listReports } from '../../lib/reports';

export const prerender = false;

function unauthorized() {
  return new Response(JSON.stringify({ message: 'Unauthorized' }), {
    status: 401,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
}

export const GET: APIRoute = async ({ request }) => {
  const expected = await feedToken();
  if (!expected) return unauthorized();
  const got = (request.headers.get('authorization') ?? '').replace(/^Bearer\s+/i, '').trim();
  if (got !== expected) return unauthorized();

  const reports = await listReports();
  return new Response(JSON.stringify({ reports }), {
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
};
