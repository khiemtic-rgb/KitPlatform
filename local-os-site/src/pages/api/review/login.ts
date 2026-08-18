import type { APIRoute } from 'astro';
import { json, localOsAdminApiBase } from '../../../lib/review-api';

export const prerender = false;

export const POST: APIRoute = async ({ request }) => {
  let username = '';
  let password = '';
  try {
    const body = (await request.json()) as { username?: string; password?: string };
    username = (body.username ?? '').trim();
    password = body.password ?? '';
  } catch {
    return json({ message: 'Nội dung gửi lên chưa đúng.' }, 400);
  }
  if (!username || !password) return json({ message: 'Nhập tài khoản và mật khẩu.' }, 400);

  try {
    const api = await localOsAdminApiBase();
    const res = await fetch(`${api}/auth/login`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ username, password, tenantCode: 'KIT_LOCAL' }),
    });
    const data = (await res.json().catch(() => ({}))) as {
      accessToken?: string;
      user?: { tenantCode?: string };
      message?: string;
    };
    if (!res.ok || !data.accessToken) {
      return json({ message: data.message || 'Không đăng nhập được.' }, res.status || 401);
    }
    const tenant = (data.user?.tenantCode ?? '').toUpperCase();
    if (tenant && tenant !== 'KIT_LOCAL') {
      return json({ message: 'Tài khoản này không duyệt Thái Nguyên Life.' }, 403);
    }
    return json({ accessToken: data.accessToken });
  } catch {
    return json({ message: 'Chưa kết nối được. Thử lại sau.' }, 502);
  }
};
