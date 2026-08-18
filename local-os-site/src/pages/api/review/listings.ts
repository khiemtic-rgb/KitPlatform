import type { APIRoute } from 'astro';
import { bearer, json, localOsAdminApiBase } from '../../../lib/review-api';

export const prerender = false;

export const GET: APIRoute = async ({ request }) => {
  const token = bearer(request);
  if (!token) return json({ message: 'Hết phiên. Đăng nhập lại.' }, 401);
  try {
    const api = await localOsAdminApiBase();
    const status = new URL(request.url).searchParams.get('status') || 'NEEDS_REVIEW';
    const res = await fetch(`${api}/local-os/listings?status=${encodeURIComponent(status)}`, {
      headers: { authorization: `Bearer ${token}` },
    });
    if (res.status === 401) return json({ message: 'Hết phiên. Đăng nhập lại.' }, 401);
    if (!res.ok) return json({ message: 'Không tải được hàng chờ.' }, res.status);
    return json(await res.json());
  } catch {
    return json({ message: 'Chưa kết nối được. Thử lại sau.' }, 502);
  }
};
