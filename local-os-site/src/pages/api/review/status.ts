import type { APIRoute } from 'astro';
import { bearer, json, localOsAdminApiBase } from '../../../lib/review-api';

export const prerender = false;

export const POST: APIRoute = async ({ request }) => {
  const token = bearer(request);
  if (!token) return json({ message: 'Hết phiên. Đăng nhập lại.' }, 401);

  let id = '';
  let status = '';
  let phone: string | undefined;
  let listing: Record<string, unknown> | undefined;
  try {
    const body = (await request.json()) as {
      id?: string;
      status?: string;
      phone?: string;
      listing?: Record<string, unknown>;
    };
    id = (body.id ?? '').trim();
    status = (body.status ?? '').trim().toUpperCase();
    phone = body.phone?.trim();
    listing = body.listing;
  } catch {
    return json({ message: 'Nội dung gửi lên chưa đúng.' }, 400);
  }
  if (!/^[0-9a-f-]{36}$/i.test(id) || !['ACTIVE', 'HIDDEN'].includes(status)) {
    return json({ message: 'Thiếu tin hoặc trạng thái.' }, 400);
  }

  try {
    const api = await localOsAdminApiBase();
    const headers = {
      authorization: `Bearer ${token}`,
      'content-type': 'application/json',
    };

    if (phone && listing) {
      const put = await fetch(`${api}/local-os/listings/${id}`, {
        method: 'PUT',
        headers,
        body: JSON.stringify({
          kind: listing.kind,
          title: listing.title,
          summary: listing.summary,
          organizationName: listing.organizationName,
          placeText: listing.placeText,
          contactPhone: phone,
          contactName: listing.contactName,
          salaryText: listing.salaryText,
          workingTime: listing.workingTime,
          employmentType: listing.employmentType,
          category: listing.category,
          requirements: listing.requirements,
          startAt: listing.startAt,
          endAt: listing.endAt,
          roomType: listing.roomType,
          trust: listing.trust,
          safetyFlag: listing.safetyFlag,
          status: listing.status,
          sourceKind: listing.sourceKind,
          sourceUrl: listing.sourceUrl,
        }),
      });
      if (put.status === 401) return json({ message: 'Hết phiên. Đăng nhập lại.' }, 401);
      if (!put.ok) {
        const err = (await put.json().catch(() => ({}))) as { message?: string };
        return json({ message: err.message || 'Không lưu được số điện thoại.' }, put.status);
      }
    }

    const res = await fetch(`${api}/local-os/listings/${id}/status`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ status }),
    });
    if (res.status === 401) return json({ message: 'Hết phiên. Đăng nhập lại.' }, 401);
    if (!res.ok) {
      const err = (await res.json().catch(() => ({}))) as { message?: string };
      return json({ message: err.message || 'Chưa đổi được trạng thái.' }, res.status);
    }
    return json(await res.json());
  } catch {
    return json({ message: 'Chưa kết nối được. Thử lại sau.' }, 502);
  }
};
