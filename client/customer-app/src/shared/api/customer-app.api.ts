import axios from 'axios';
import { http } from '@/shared/api/http';
import type {
  CreateMedicationReminderRequest,
  CustomerChatMessageList,
  CustomerChatThread,
  CustomerConsent,
  CustomerLoginResponse,
  CustomerProductSearchResult,
  CustomerProfile,
  CustomerVoucherList,
  LoyaltySummary,
  MedicationReminderList,
  PushSubscriptionStatus,
  PagedLoyaltyTransactions,
  UpdateMedicationReminderRequest,
} from '@/shared/api/customer-app.types';
import { normalizeReminder, normalizeReminderList } from '@/shared/api/reminder-normalize';

export async function requestOtp(phone: string, tenantCode: string) {
  const { data } = await http.post<{ expiresInSeconds: number; message: string }>('/auth/request-otp', {
    phone,
    tenantCode,
  });
  return data;
}

export async function verifyOtp(phone: string, code: string, tenantCode: string) {
  const { data } = await http.post<CustomerLoginResponse>('/auth/verify-otp', {
    phone,
    code,
    tenantCode,
  });
  return data;
}

export async function logoutApi(refreshToken: string) {
  await http.post('/auth/logout', { refreshToken });
}

export async function fetchProfile() {
  const { data } = await http.get<CustomerProfile>('/auth/me');
  return data;
}

export async function fetchLoyaltySummary() {
  const { data } = await http.get<LoyaltySummary>('/loyalty/summary');
  return data;
}

export async function fetchLoyaltyTransactions(page = 1, pageSize = 20) {
  const { data } = await http.get<PagedLoyaltyTransactions>('/loyalty/transactions', {
    params: { page, pageSize },
  });
  return data;
}

export async function fetchVouchers(includeUsed = false) {
  const { data } = await http.get<CustomerVoucherList>('/loyalty/vouchers', {
    params: { includeUsed },
  });
  return data;
}

export async function fetchReminders(includeInactive = false) {
  const { data } = await http.get<MedicationReminderList>('/reminders', {
    params: { includeInactive },
  });
  return { items: normalizeReminderList(data) };
}

export async function createReminder(payload: CreateMedicationReminderRequest) {
  const { data } = await http.post<Record<string, unknown>>('/reminders', payload);
  return normalizeReminder(data);
}

export async function updateReminder(id: string, payload: UpdateMedicationReminderRequest) {
  const { data } = await http.put<Record<string, unknown>>(`/reminders/${id}`, payload);
  return normalizeReminder(data);
}

export async function deactivateReminder(id: string) {
  await http.delete(`/reminders/${id}`);
}

export async function searchProducts(search?: string, page = 1, pageSize = 20) {
  const { data } = await http.get<CustomerProductSearchResult>('/catalog/products', {
    params: { search: search?.trim() || undefined, page, pageSize },
  });
  return data;
}

function normalizeGranted(row: Record<string, unknown>): boolean {
  const raw = row.granted ?? row.Granted;
  if (raw === true || raw === 'true') return true;
  if (raw === false || raw === 'false') return false;
  return Boolean(raw);
}

function normalizeConsent(row: Record<string, unknown>): CustomerConsent {
  return {
    id: String(row.id ?? row.Id),
    customerId: String(row.customerId ?? row.CustomerId),
    channel: Number(row.channel ?? row.Channel ?? 1),
    purpose: Number(row.purpose ?? row.Purpose ?? 1),
    granted: normalizeGranted(row),
    grantedAt: (row.grantedAt ?? row.GrantedAt) as string | undefined,
    revokedAt: (row.revokedAt ?? row.RevokedAt) as string | undefined,
    source: Number(row.source ?? row.Source ?? 3),
    notes: (row.notes ?? row.Notes) as string | undefined,
  };
}

export async function fetchConsents() {
  const { data } = await http.get<Record<string, unknown>[]>('/consents');
  return data.map(normalizeConsent);
}

export async function upsertConsents(
  items: { channel: number; purpose: number; granted: boolean; notes?: string }[],
) {
  const { data } = await http.put<Record<string, unknown>[]>('/consents', { items });
  return data.map(normalizeConsent);
}

export async function fetchCareReminderEligible() {
  const { data } = await http.get<{ eligible: boolean }>('/consents/care-reminder-eligible');
  return data.eligible;
}

export async function fetchPushStatus() {
  const { data } = await http.get<PushSubscriptionStatus>('/push/status');
  return data;
}

export async function registerPushSubscription(payload: {
  endpoint: string;
  p256dh: string;
  auth: string;
}) {
  await http.post('/push/subscribe', payload);
}

export async function unregisterPushSubscription(endpoint: string) {
  await http.delete('/push/subscribe', { params: { endpoint } });
}

function normalizeChatMessage(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    senderType: Number(row.senderType ?? row.SenderType ?? 1),
    senderName: (row.senderName ?? row.SenderName) as string | null,
    body: String(row.body ?? row.Body ?? ''),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    readAt: (row.readAt ?? row.ReadAt) as string | null,
  };
}

export async function fetchChatThread() {
  const { data } = await http.get<Record<string, unknown>>('/chat/thread');
  return {
    threadId: String(data.threadId ?? data.ThreadId ?? ''),
    unreadCount: Number(data.unreadCount ?? data.UnreadCount ?? 0),
    lastMessageAt: (data.lastMessageAt ?? data.LastMessageAt) as string | null,
    lastMessagePreview: (data.lastMessagePreview ?? data.LastMessagePreview) as string | null,
  } satisfies CustomerChatThread;
}

export async function fetchChatMessages(beforeId?: string, limit = 50) {
  const { data } = await http.get<Record<string, unknown>>('/chat/messages', {
    params: { beforeId, limit },
  });
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(normalizeChatMessage);
  return {
    items,
    hasMore: Boolean(data.hasMore ?? data.HasMore),
  } satisfies CustomerChatMessageList;
}

export async function sendChatMessage(body: string) {
  const { data } = await http.post<Record<string, unknown>>('/chat/messages', { body });
  return normalizeChatMessage(data);
}

export async function markChatRead() {
  await http.post('/chat/read');
}

function normalizeDraftOrderLine(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    lineNumber: Number(row.lineNumber ?? row.LineNumber ?? 0),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    lineAmount: Number(row.lineAmount ?? row.LineAmount ?? 0),
    dosageNote: (row.dosageNote ?? row.DosageNote) as string | null | undefined,
  };
}

function normalizeDraftOrder(row: Record<string, unknown>) {
  const items = ((row.items ?? row.Items ?? []) as Record<string, unknown>[]).map(normalizeDraftOrderLine);
  return {
    id: String(row.id ?? row.Id),
    draftNumber: String(row.draftNumber ?? row.DraftNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    subtotal: Number(row.subtotal ?? row.Subtotal ?? 0),
    discountAmount: Number(row.discountAmount ?? row.DiscountAmount ?? 0),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    notes: (row.notes ?? row.Notes) as string | null | undefined,
    sentAt: (row.sentAt ?? row.SentAt) as string | null | undefined,
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    completedAt: (row.completedAt ?? row.CompletedAt) as string | null | undefined,
    expiresAt: (row.expiresAt ?? row.ExpiresAt) as string | null | undefined,
    salesOrderNumber: (row.salesOrderNumber ?? row.SalesOrderNumber) as string | null | undefined,
    items,
  };
}

function normalizeDraftOrderListItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    draftNumber: String(row.draftNumber ?? row.DraftNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    sentAt: (row.sentAt ?? row.SentAt) as string | null | undefined,
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    expiresAt: (row.expiresAt ?? row.ExpiresAt) as string | null | undefined,
  };
}

export async function fetchDraftOrders() {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/draft-orders',
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizeDraftOrderListItem);
}

export async function fetchDraftOrder(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/draft-orders/${id}`);
  return normalizeDraftOrder(data);
}

export async function confirmDraftOrder(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/draft-orders/${id}/confirm`);
  return normalizeDraftOrder(data);
}

export async function hideDraftOrder(id: string) {
  await http.post(`/draft-orders/${id}/hide`);
}

export async function cancelDraftOrder(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/draft-orders/${id}/cancel`);
  return normalizeDraftOrder(data);
}

export function getApiErrorMessage(error: unknown, fallback = 'Đã có lỗi xảy ra') {
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return 'Không kết nối được API. Chạy run-dev.bat hoặc .\\scripts\\restart-api.ps1 trước.';
    }
    const data = error.response.data;
    if (typeof data === 'string' && data.trim()) return data;
    if (data && typeof data === 'object' && 'message' in data) {
      const msg = String((data as { message?: string }).message ?? '');
      if (msg.trim()) return msg;
    }
    if (error.response.status === 500) {
      return 'API đang lỗi hoặc chưa chạy. Chạy .\\scripts\\restart-api.ps1 (hoặc run-dev.bat) rồi thử lại.';
    }
    if (error.response.status === 502 || error.response.status === 503) {
      return 'Không kết nối được API. Chạy run-dev.bat hoặc .\\scripts\\restart-api.ps1 trước.';
    }
    return `${fallback} (HTTP ${error.response.status})`;
  }
  if (error instanceof Error && error.message) {
    if (/applicationServerKey is not valid/i.test(error.message)) {
      return 'VAPID key không hợp lệ. Restart API (scripts\\restart-api.ps1) để nạp CustomerAppPush mới.';
    }
    return error.message;
  }
  return fallback;
}
