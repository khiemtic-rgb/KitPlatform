import { http } from '@/shared/api/http';

export type AuditLogItem = {
  id: string;
  userId?: string;
  username?: string;
  entityType: string;
  entityId?: string;
  action: string;
  payloadJson?: string;
  createdAt: string;
};

export type PagedAuditLogs = {
  items: AuditLogItem[];
  total: number;
  page: number;
  pageSize: number;
};

function normalizeItem(row: Record<string, unknown>): AuditLogItem {
  return {
    id: String(row.id ?? row.Id),
    userId: (row.userId ?? row.UserId) as string | undefined,
    username: (row.username ?? row.Username) as string | undefined,
    entityType: String(row.entityType ?? row.EntityType ?? ''),
    entityId: (row.entityId ?? row.EntityId) as string | undefined,
    action: String(row.action ?? row.Action ?? ''),
    payloadJson: (row.payloadJson ?? row.PayloadJson) as string | undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchAuditLogs(params?: {
  entityType?: string;
  action?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedAuditLogs> {
  const { data } = await http.get<Record<string, unknown>>('/system/audit-log', { params });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    items: rawItems.map(normalizeItem),
    total: Number(data.total ?? data.Total ?? 0),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? 50),
  };
}

export const AUDIT_ENTITY_LABELS: Record<string, string> = {
  sales_order: 'Bán hàng',
  goods_receipt: 'Nhập kho (GRN)',
  purchase_order: 'Đơn mua',
  supplier_payment: 'Thanh toán NCC',
  opening_balance: 'Tồn đầu kỳ',
  inventory_adjustment: 'Kiểm kê',
};

export const AUDIT_ACTION_LABELS: Record<string, string> = {
  complete: 'Hoàn tất',
  draft_create: 'Tạo nháp',
  draft_update: 'Cập nhật nháp',
  cancel: 'Hủy',
  approve: 'Duyệt',
  create: 'Tạo mới',
  post: 'Ghi sổ',
};
