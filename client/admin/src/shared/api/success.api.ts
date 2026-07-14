import { http } from '@/shared/api/http';
import type { DashboardOverview } from '@/shared/api/dashboard.types';

export interface OwnerCockpitRiskStrip {
  cashVarianceThreshold: number;
  closedShiftCountToday: number;
  openShiftCountToday: number;
  cashVarianceAlertCount: number;
  maxAbsCashVarianceToday: number;
  topAlertShiftNumber?: string | null;
}

export interface OwnerCockpit {
  overview: DashboardOverview;
  salesExtras: {
    monthNetTotal: number;
    weekOrderCount: number;
    monthOrderCount: number;
  };
  inventoryExtras: {
    nearExpirySkuCount: number;
    nearExpiryStockValue: number;
  };
  customers: {
    newCustomers7d: number;
    returningCustomers7d: number;
  };
  latestAssessment?: {
    submissionId: string;
    overallScore?: number | null;
    completedAt?: string | null;
    status: string;
  } | null;
  riskStrip?: OwnerCockpitRiskStrip | null;
}

export interface LossCashVarianceShift {
  shiftId: string;
  shiftNumber: string;
  warehouseId: string;
  warehouseName: string;
  branchId: string;
  branchName: string;
  status: string;
  openingCash: number;
  closingCash?: number | null;
  expectedCash?: number | null;
  cashVariance?: number | null;
  absCashVariance: number;
  isAlert: boolean;
  openedAt: string;
  closedAt?: string | null;
}

export interface LossCashVarianceToday {
  businessDate: string;
  threshold: number;
  closedShiftCount: number;
  openShiftCount: number;
  alertCount: number;
  maxAbsVariance: number;
  shifts: LossCashVarianceShift[];
}

type UnknownRow = Record<string, unknown>;

function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}

function normalizeOverview(row: UnknownRow): DashboardOverview {
  const sales = (row.sales ?? row.Sales ?? {}) as UnknownRow;
  const catalog = (row.catalog ?? row.Catalog ?? {}) as UnknownRow;
  const inventory = (row.inventory ?? row.Inventory ?? {}) as UnknownRow;
  const procurement = (row.procurement ?? row.Procurement ?? {}) as UnknownRow;
  const o2o = (row.o2o ?? row.O2o ?? {}) as UnknownRow;
  return {
    sales: {
      todayNetTotal: num(sales.todayNetTotal ?? sales.TodayNetTotal),
      weekNetTotal: num(sales.weekNetTotal ?? sales.WeekNetTotal),
      todayOrderCount: num(sales.todayOrderCount ?? sales.TodayOrderCount),
    },
    catalog: {
      productCount: num(catalog.productCount ?? catalog.ProductCount),
      customerCount: num(catalog.customerCount ?? catalog.CustomerCount),
    },
    inventory: {
      activeBatchCount: num(inventory.activeBatchCount ?? inventory.ActiveBatchCount),
      nearExpiryBatchCount: num(inventory.nearExpiryBatchCount ?? inventory.NearExpiryBatchCount),
      lowStockBatchCount: num(inventory.lowStockBatchCount ?? inventory.LowStockBatchCount),
      lowStockProductCount: num(inventory.lowStockProductCount ?? inventory.LowStockProductCount),
      expiryDays: num(inventory.expiryDays ?? inventory.ExpiryDays) || 30,
    },
    procurement: {
      pendingReceiptCount: num(procurement.pendingReceiptCount ?? procurement.PendingReceiptCount),
    },
    o2o: {
      draftOrdersAwaitingCount: num(o2o.draftOrdersAwaitingCount ?? o2o.DraftOrdersAwaitingCount),
      reservationsAwaitingCount: num(o2o.reservationsAwaitingCount ?? o2o.ReservationsAwaitingCount),
      chatUnreadCount: num(o2o.chatUnreadCount ?? o2o.ChatUnreadCount),
    },
  };
}

export type ShiftChecklistKind = 'open' | 'close';

export interface ShiftChecklistKindStatus {
  kind: ShiftChecklistKind;
  status: string;
  runId?: string | null;
  checkedCount: number;
  totalCount: number;
  requiredMissingCount: number;
  completedAt?: string | null;
}

export interface ShiftChecklistToday {
  businessDate: string;
  branchId?: string | null;
  branches: { id: string; name: string; code?: string | null }[];
  open: ShiftChecklistKindStatus;
  close: ShiftChecklistKindStatus;
}

export interface ShiftChecklistRunItem {
  id: string;
  label: string;
  isRequired: boolean;
  isChecked: boolean;
  checkedAt?: string | null;
}

export interface ShiftChecklistRun {
  id: string;
  branchId: string;
  branchName: string;
  kind: ShiftChecklistKind;
  businessDate: string;
  status: string;
  startedAt: string;
  completedAt?: string | null;
  items: ShiftChecklistRunItem[];
}

function normalizeKindStatus(row: UnknownRow, fallback: ShiftChecklistKind): ShiftChecklistKindStatus {
  const kind = String(row.kind ?? row.Kind ?? fallback).toLowerCase() as ShiftChecklistKind;
  return {
    kind: kind === 'close' ? 'close' : 'open',
    status: String(row.status ?? row.Status ?? 'missing'),
    runId: (row.runId ?? row.RunId) as string | null | undefined,
    checkedCount: num(row.checkedCount ?? row.CheckedCount),
    totalCount: num(row.totalCount ?? row.TotalCount),
    requiredMissingCount: num(row.requiredMissingCount ?? row.RequiredMissingCount),
    completedAt: (row.completedAt ?? row.CompletedAt) as string | null | undefined,
  };
}

function normalizeRun(row: UnknownRow): ShiftChecklistRun {
  const itemsRaw = (row.items ?? row.Items ?? []) as UnknownRow[];
  const kind = String(row.kind ?? row.Kind ?? 'open').toLowerCase() as ShiftChecklistKind;
  return {
    id: String(row.id ?? row.Id ?? ''),
    branchId: String(row.branchId ?? row.BranchId ?? ''),
    branchName: String(row.branchName ?? row.BranchName ?? ''),
    kind: kind === 'close' ? 'close' : 'open',
    businessDate: String(row.businessDate ?? row.BusinessDate ?? ''),
    status: String(row.status ?? row.Status ?? ''),
    startedAt: String(row.startedAt ?? row.StartedAt ?? ''),
    completedAt: (row.completedAt ?? row.CompletedAt) as string | null | undefined,
    items: itemsRaw.map((item) => ({
      id: String(item.id ?? item.Id ?? ''),
      label: String(item.label ?? item.Label ?? ''),
      isRequired: Boolean(item.isRequired ?? item.IsRequired),
      isChecked: Boolean(item.isChecked ?? item.IsChecked),
      checkedAt: (item.checkedAt ?? item.CheckedAt) as string | null | undefined,
    })),
  };
}

export async function fetchShiftChecklistToday(branchId?: string): Promise<ShiftChecklistToday> {
  const { data } = await http.get<UnknownRow>('/success/shift-checklist/today', {
    params: branchId ? { branchId } : undefined,
  });
  const row = data as UnknownRow;
  const branches = ((row.branches ?? row.Branches ?? []) as UnknownRow[]).map((b) => ({
    id: String(b.id ?? b.Id ?? ''),
    name: String(b.name ?? b.Name ?? ''),
    code: (b.code ?? b.Code) as string | null | undefined,
  }));
  return {
    businessDate: String(row.businessDate ?? row.BusinessDate ?? ''),
    branchId: (row.branchId ?? row.BranchId) as string | null | undefined,
    branches,
    open: normalizeKindStatus((row.open ?? row.Open ?? {}) as UnknownRow, 'open'),
    close: normalizeKindStatus((row.close ?? row.Close ?? {}) as UnknownRow, 'close'),
  };
}

export async function startShiftChecklistRun(branchId: string, kind: ShiftChecklistKind): Promise<ShiftChecklistRun> {
  const { data } = await http.post<UnknownRow>('/success/shift-checklist/runs', { branchId, kind });
  return normalizeRun(data as UnknownRow);
}

export async function setShiftChecklistItem(
  runId: string,
  itemId: string,
  checked: boolean,
): Promise<ShiftChecklistRun> {
  const { data } = await http.put<UnknownRow>(`/success/shift-checklist/runs/${runId}/items/${itemId}`, {
    checked,
  });
  return normalizeRun(data as UnknownRow);
}

export async function completeShiftChecklistRun(runId: string): Promise<ShiftChecklistRun> {
  const { data } = await http.post<UnknownRow>(`/success/shift-checklist/runs/${runId}/complete`);
  return normalizeRun(data as UnknownRow);
}

export async function fetchLossCashVarianceToday(threshold?: number): Promise<LossCashVarianceToday> {
  const { data } = await http.get<UnknownRow>('/success/loss/cash-variance', {
    params: threshold != null ? { threshold } : undefined,
  });
  const row = data as UnknownRow;
  const shifts = ((row.shifts ?? row.Shifts ?? []) as UnknownRow[]).map((s) => ({
    shiftId: String(s.shiftId ?? s.ShiftId ?? ''),
    shiftNumber: String(s.shiftNumber ?? s.ShiftNumber ?? ''),
    warehouseId: String(s.warehouseId ?? s.WarehouseId ?? ''),
    warehouseName: String(s.warehouseName ?? s.WarehouseName ?? ''),
    branchId: String(s.branchId ?? s.BranchId ?? ''),
    branchName: String(s.branchName ?? s.BranchName ?? ''),
    status: String(s.status ?? s.Status ?? ''),
    openingCash: num(s.openingCash ?? s.OpeningCash),
    closingCash: (s.closingCash ?? s.ClosingCash) as number | null | undefined,
    expectedCash: (s.expectedCash ?? s.ExpectedCash) as number | null | undefined,
    cashVariance: (s.cashVariance ?? s.CashVariance) as number | null | undefined,
    absCashVariance: num(s.absCashVariance ?? s.AbsCashVariance),
    isAlert: Boolean(s.isAlert ?? s.IsAlert),
    openedAt: String(s.openedAt ?? s.OpenedAt ?? ''),
    closedAt: (s.closedAt ?? s.ClosedAt) as string | null | undefined,
  }));
  return {
    businessDate: String(row.businessDate ?? row.BusinessDate ?? ''),
    threshold: num(row.threshold ?? row.Threshold),
    closedShiftCount: num(row.closedShiftCount ?? row.ClosedShiftCount),
    openShiftCount: num(row.openShiftCount ?? row.OpenShiftCount),
    alertCount: num(row.alertCount ?? row.AlertCount),
    maxAbsVariance: num(row.maxAbsVariance ?? row.MaxAbsVariance),
    shifts,
  };
}

export async function fetchOwnerCockpit(params?: {
  expiryDays?: number;
  lowStockThreshold?: number;
}): Promise<OwnerCockpit> {
  const { data } = await http.get<UnknownRow>('/success/owner-cockpit', { params });
  const row = data as UnknownRow;
  const salesExtras = (row.salesExtras ?? row.SalesExtras ?? {}) as UnknownRow;
  const inventoryExtras = (row.inventoryExtras ?? row.InventoryExtras ?? {}) as UnknownRow;
  const customers = (row.customers ?? row.Customers ?? {}) as UnknownRow;
  const assessment = (row.latestAssessment ?? row.LatestAssessment) as UnknownRow | null | undefined;
  const risk = (row.riskStrip ?? row.RiskStrip) as UnknownRow | null | undefined;

  return {
    overview: normalizeOverview((row.overview ?? row.Overview ?? {}) as UnknownRow),
    salesExtras: {
      monthNetTotal: num(salesExtras.monthNetTotal ?? salesExtras.MonthNetTotal),
      weekOrderCount: num(salesExtras.weekOrderCount ?? salesExtras.WeekOrderCount),
      monthOrderCount: num(salesExtras.monthOrderCount ?? salesExtras.MonthOrderCount),
    },
    inventoryExtras: {
      nearExpirySkuCount: num(inventoryExtras.nearExpirySkuCount ?? inventoryExtras.NearExpirySkuCount),
      nearExpiryStockValue: num(
        inventoryExtras.nearExpiryStockValue ?? inventoryExtras.NearExpiryStockValue,
      ),
    },
    customers: {
      newCustomers7d: num(customers.newCustomers7d ?? customers.NewCustomers7d),
      returningCustomers7d: num(customers.returningCustomers7d ?? customers.ReturningCustomers7d),
    },
    latestAssessment: assessment
      ? {
          submissionId: String(assessment.submissionId ?? assessment.SubmissionId ?? ''),
          overallScore:
            assessment.overallScore == null && assessment.OverallScore == null
              ? null
              : num(assessment.overallScore ?? assessment.OverallScore),
          completedAt: (assessment.completedAt ?? assessment.CompletedAt) as string | null | undefined,
          status: String(assessment.status ?? assessment.Status ?? ''),
        }
      : null,
    riskStrip: risk
      ? {
          cashVarianceThreshold: num(risk.cashVarianceThreshold ?? risk.CashVarianceThreshold),
          closedShiftCountToday: num(risk.closedShiftCountToday ?? risk.ClosedShiftCountToday),
          openShiftCountToday: num(risk.openShiftCountToday ?? risk.OpenShiftCountToday),
          cashVarianceAlertCount: num(risk.cashVarianceAlertCount ?? risk.CashVarianceAlertCount),
          maxAbsCashVarianceToday: num(risk.maxAbsCashVarianceToday ?? risk.MaxAbsCashVarianceToday),
          topAlertShiftNumber: (risk.topAlertShiftNumber ?? risk.TopAlertShiftNumber) as
            | string
            | null
            | undefined,
        }
      : null,
  };
}
