import { http } from '@/shared/api/http';

export type CustomerEngagementFunnelStep = {
  key: string;
  label: string;
  count: number;
  rateFromCohort: number;
  rateFromPrevious: number;
  deltaVsPriorPeriod: number;
};

export type CustomerEngagementOverview = {
  periodDays: number;
  cohortSize: number;
  funnel: CustomerEngagementFunnelStep[];
  retention30d: {
    eligibleCount: number;
    retainedCount: number;
    rate: number;
    deltaVsPriorPeriod: number;
  };
  alerts: Array<{ key: string; severity: string; message: string }>;
};

export type CustomerEngagementDrillDownItem = {
  accountId: string;
  customerId: string;
  fullName: string;
  phone: string;
  lastLoginAt: string | null;
  firstLoginAt: string | null;
};

export type CustomerEngagementDrillDown = {
  step: string;
  total: number;
  page: number;
  pageSize: number;
  items: CustomerEngagementDrillDownItem[];
};

function num(row: Record<string, unknown>, ...keys: string[]): number {
  for (const key of keys) {
    if (row[key] !== undefined && row[key] !== null) return Number(row[key]);
  }
  return 0;
}

function str(row: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = row[key];
    if (value !== undefined && value !== null) return String(value);
  }
  return '';
}

function normalizeFunnelStep(row: Record<string, unknown>): CustomerEngagementFunnelStep {
  return {
    key: str(row, 'key', 'Key'),
    label: str(row, 'label', 'Label'),
    count: num(row, 'count', 'Count'),
    rateFromCohort: num(row, 'rateFromCohort', 'RateFromCohort'),
    rateFromPrevious: num(row, 'rateFromPrevious', 'RateFromPrevious'),
    deltaVsPriorPeriod: num(row, 'deltaVsPriorPeriod', 'DeltaVsPriorPeriod'),
  };
}

function normalizeDrillItem(row: Record<string, unknown>): CustomerEngagementDrillDownItem {
  return {
    accountId: str(row, 'accountId', 'AccountId'),
    customerId: str(row, 'customerId', 'CustomerId'),
    fullName: str(row, 'fullName', 'FullName'),
    phone: str(row, 'phone', 'Phone'),
    lastLoginAt: (row.lastLoginAt ?? row.LastLoginAt ?? null) as string | null,
    firstLoginAt: (row.firstLoginAt ?? row.FirstLoginAt ?? null) as string | null,
  };
}

export async function fetchCustomerEngagementOverview(periodDays = 30): Promise<CustomerEngagementOverview> {
  const { data } = await http.get<Record<string, unknown>>('/customer-engagement/overview', {
    params: { periodDays },
  });

  const funnelRaw = (data.funnel ?? data.Funnel ?? []) as Record<string, unknown>[];
  const retention = (data.retention30d ?? data.Retention30d ?? {}) as Record<string, unknown>;
  const alertsRaw = (data.alerts ?? data.Alerts ?? []) as Record<string, unknown>[];

  return {
    periodDays: num(data, 'periodDays', 'PeriodDays') || periodDays,
    cohortSize: num(data, 'cohortSize', 'CohortSize'),
    funnel: funnelRaw.map(normalizeFunnelStep),
    retention30d: {
      eligibleCount: num(retention, 'eligibleCount', 'EligibleCount'),
      retainedCount: num(retention, 'retainedCount', 'RetainedCount'),
      rate: num(retention, 'rate', 'Rate'),
      deltaVsPriorPeriod: num(retention, 'deltaVsPriorPeriod', 'DeltaVsPriorPeriod'),
    },
    alerts: alertsRaw.map((row) => ({
      key: str(row, 'key', 'Key'),
      severity: str(row, 'severity', 'Severity'),
      message: str(row, 'message', 'Message'),
    })),
  };
}

export async function fetchCustomerEngagementDrillDown(params: {
  step: string;
  periodDays?: number;
  page?: number;
  pageSize?: number;
}): Promise<CustomerEngagementDrillDown> {
  const { data } = await http.get<Record<string, unknown>>('/customer-engagement/drill-down', { params });
  const itemsRaw = (data.items ?? data.Items ?? []) as Record<string, unknown>[];

  return {
    step: str(data, 'step', 'Step'),
    total: num(data, 'total', 'Total'),
    page: num(data, 'page', 'Page') || 1,
    pageSize: num(data, 'pageSize', 'PageSize') || 50,
    items: itemsRaw.map(normalizeDrillItem),
  };
}
