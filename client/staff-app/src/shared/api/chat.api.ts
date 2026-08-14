import { http } from '@/shared/api/http';
import type { ChatMessage, ChatThread } from '@/shared/api/chat.types';

function normalizeThread(row: Record<string, unknown>): ChatThread {
  return {
    threadId: String(row.threadId ?? row.ThreadId),
    customerId: String(row.customerId ?? row.CustomerId),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    customerPhone: (row.customerPhone ?? row.CustomerPhone) as string | null,
    staffUnreadCount: Number(row.staffUnreadCount ?? row.StaffUnreadCount ?? 0),
    lastMessageAt: (row.lastMessageAt ?? row.LastMessageAt) as string | null,
    lastMessagePreview: (row.lastMessagePreview ?? row.LastMessagePreview) as string | null,
  };
}

function normalizeMessage(row: Record<string, unknown>): ChatMessage {
  return {
    id: String(row.id ?? row.Id),
    senderType: Number(row.senderType ?? row.SenderType ?? 1),
    senderName: (row.senderName ?? row.SenderName) as string | null,
    body: String(row.body ?? row.Body ?? ''),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    readAt: (row.readAt ?? row.ReadAt) as string | null,
  };
}

export async function fetchChatThreads(): Promise<ChatThread[]> {
  const { data } = await http.get<
    Record<string, unknown> | Record<string, unknown>[] | { items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }
  >('/sales/customer-chat/threads');
  const rows = Array.isArray(data)
    ? data
    : ((data.items ?? data.Items ?? []) as Record<string, unknown>[]);
  return rows.map(normalizeThread).filter((t) => t.customerId && t.customerId !== 'undefined');
}

export async function fetchChatMessages(customerId: string, limit = 50) {
  const { data } = await http.get<Record<string, unknown>>(
    `/sales/customer-chat/threads/${customerId}/messages`,
    { params: { limit } },
  );
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(normalizeMessage);
  return { items, hasMore: Boolean(data.hasMore ?? data.HasMore) };
}

export async function sendChatMessage(customerId: string, body: string): Promise<ChatMessage> {
  const { data } = await http.post<Record<string, unknown>>(
    `/sales/customer-chat/threads/${customerId}/messages`,
    { body },
  );
  return normalizeMessage(data);
}

export async function markChatRead(customerId: string): Promise<void> {
  await http.post(`/sales/customer-chat/threads/${customerId}/read`);
}

export function sumUnreadThreads(threads: ChatThread[]): number {
  return threads.reduce((sum, t) => sum + t.staffUnreadCount, 0);
}
