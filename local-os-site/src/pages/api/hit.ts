import type { APIRoute } from 'astro';
import { isBot, normalizePath, recordHit } from '../../lib/hits';

export const prerender = false;

export const POST: APIRoute = async ({ request }) => {
  if (isBot(request.headers.get('user-agent'))) {
    return new Response(null, { status: 204 });
  }
  let path: string | null = null;
  try {
    const body = (await request.json()) as { path?: string };
    path = normalizePath(body.path);
  } catch {
    path = null;
  }
  if (path) await recordHit(path);
  return new Response(null, { status: 204 });
};
