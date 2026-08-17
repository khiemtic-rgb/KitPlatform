import type { APIRoute } from 'astro';
import { localOsApiBase } from '../../lib/backend-api';

export const prerender = false;

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
}

export const POST: APIRoute = async ({ request }) => {
  let payload: unknown;
  try {
    payload = await request.json();
  } catch {
    return json({ message: 'Nội dung gửi lên chưa đúng.' }, 400);
  }

  try {
    const api = await localOsApiBase();
    const res = await fetch(`${api}/listings/jobs`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const text = await res.text();
    if (!text) {
      return json(
        { message: res.ok ? 'Đã nhận tin.' : 'Chưa gửi được. Thử lại sau một lúc.' },
        res.ok ? 200 : res.status,
      );
    }
    return new Response(text, {
      status: res.status,
      headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
    });
  } catch {
    return json({ message: 'Chưa gửi được. Thử lại sau một lúc.' }, 502);
  }
};
