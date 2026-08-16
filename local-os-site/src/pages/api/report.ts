import type { APIRoute } from 'astro';
import { appendReport, hasKvReports } from '../../lib/reports';

export const prerender = false;

const REASONS = new Set(['wrong_phone', 'gone', 'no_answer', 'other']);
const API = (import.meta.env.PUBLIC_LOCAL_OS_API as string | undefined)?.replace(/\/$/, '')
  || 'http://127.0.0.1:5290/api/public/local-os';

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' },
  });
}

export const POST: APIRoute = async ({ request }) => {
  let listingId = '';
  let reason = '';
  let note: string | null = null;
  try {
    const body = (await request.json()) as { listingId?: string; reason?: string; note?: string };
    listingId = (body.listingId ?? '').trim();
    reason = (body.reason ?? '').trim().toLowerCase();
    note = body.note?.trim() ? body.note.trim().slice(0, 280) : null;
  } catch {
    return json({ message: 'JSON không hợp lệ.' }, 400);
  }
  if (!/^[0-9a-f-]{36}$/i.test(listingId) || !REASONS.has(reason)) {
    return json({ message: 'Chọn lý do báo tin.' }, 400);
  }

  if (await hasKvReports()) {
    try {
      await appendReport({ listingId, reason, note });
      return json({ ok: true });
    } catch {
      return json({ message: 'Không ghi được báo cáo.' }, 500);
    }
  }

  try {
    const res = await fetch(`${API}/listings/${listingId}/reports`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ reason, note }),
    });
    if (res.status === 404) return json({ message: 'Tin không còn trên site.' }, 404);
    if (!res.ok) {
      const err = (await res.json().catch(() => ({}))) as { message?: string };
      return json({ message: err.message || 'Không gửi được báo cáo.' }, res.status);
    }
    return json({ ok: true });
  } catch {
    return json({ message: 'Không gửi được báo cáo.' }, 502);
  }
};
