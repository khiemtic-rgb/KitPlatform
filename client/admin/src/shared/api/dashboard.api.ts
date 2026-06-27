import { http } from '@/shared/api/http';
import type { DashboardOverview } from '@/shared/api/dashboard.types';

function num(row: Record<string, unknown>, ...keys: string[]): number {
  for (const key of keys) {
    if (row[key] !== undefined && row[key] !== null) return Number(row[key]);
  }
  return 0;
}

function normalizeSales(data: Record<string, unknown>) {
  return {
    todayNetTotal: num(data, 'todayNetTotal', 'TodayNetTotal'),
    weekNetTotal: num(data, 'weekNetTotal', 'WeekNetTotal'),
    todayOrderCount: num(data, 'todayOrderCount', 'TodayOrderCount'),
  };
}

function normalizeCatalog(data: Record<string, unknown>) {
  return {
    productCount: num(data, 'productCount', 'ProductCount'),
    customerCount: num(data, 'customerCount', 'CustomerCount'),
  };
}

function normalizeInventory(data: Record<string, unknown>) {
  return {
    activeBatchCount: num(data, 'activeBatchCount', 'ActiveBatchCount'),
    nearExpiryBatchCount: num(data, 'nearExpiryBatchCount', 'NearExpiryBatchCount'),
    lowStockBatchCount: num(data, 'lowStockBatchCount', 'LowStockBatchCount'),
    lowStockProductCount: num(data, 'lowStockProductCount', 'LowStockProductCount'),
    expiryDays: num(data, 'expiryDays', 'ExpiryDays') || 30,
  };
}

function normalizeProcurement(data: Record<string, unknown>) {
  return {
    pendingReceiptCount: num(data, 'pendingReceiptCount', 'PendingReceiptCount'),
  };
}

function normalizeO2o(data: Record<string, unknown>) {
  return {
    draftOrdersAwaitingCount: num(data, 'draftOrdersAwaitingCount', 'DraftOrdersAwaitingCount'),
    reservationsAwaitingCount: num(data, 'reservationsAwaitingCount', 'ReservationsAwaitingCount'),
    chatUnreadCount: num(data, 'chatUnreadCount', 'ChatUnreadCount'),
  };
}

export async function fetchDashboardOverview(params?: {
  expiryDays?: number;
}): Promise<DashboardOverview> {
  const { data } = await http.get<Record<string, unknown>>('/dashboard/overview', { params });
  const sales = (data.sales ?? data.Sales ?? {}) as Record<string, unknown>;
  const catalog = (data.catalog ?? data.Catalog ?? {}) as Record<string, unknown>;
  const inventory = (data.inventory ?? data.Inventory ?? {}) as Record<string, unknown>;
  const procurement = (data.procurement ?? data.Procurement ?? {}) as Record<string, unknown>;
  const o2o = (data.o2o ?? data.O2o ?? {}) as Record<string, unknown>;

  return {
    sales: normalizeSales(sales),
    catalog: normalizeCatalog(catalog),
    inventory: normalizeInventory(inventory),
    procurement: normalizeProcurement(procurement),
    o2o: normalizeO2o(o2o),
  };
}
