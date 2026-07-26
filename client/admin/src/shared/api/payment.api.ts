import { http } from '@/shared/api/http';

type UnknownRow = Record<string, unknown>;

export interface PaymentPlan {
  productCode: string;
  planCode: string;
  displayName: string;
  amountVnd: number;
  currency: string;
  intervalDays: number;
  trialDays: number;
}

function mapPlan(row: UnknownRow): PaymentPlan {
  return {
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    planCode: String(row.planCode ?? row.PlanCode ?? ''),
    displayName: String(row.displayName ?? row.DisplayName ?? ''),
    amountVnd: Number(row.amountVnd ?? row.AmountVnd ?? 0),
    currency: String(row.currency ?? row.Currency ?? 'VND'),
    intervalDays: Number(row.intervalDays ?? row.IntervalDays ?? 30),
    trialDays: Number(row.trialDays ?? row.TrialDays ?? 0),
  };
}

export async function listPaymentPlans(productCode: string): Promise<PaymentPlan[]> {
  const { data } = await http.get<unknown>('/payment/plans', { params: { productCode } });
  return (Array.isArray(data) ? data : []).map((row) => mapPlan(row as UnknownRow));
}

export async function updatePaymentPlan(
  productCode: string,
  planCode: string,
  payload: { amountVnd?: number; trialDays?: number; displayName?: string; isActive?: boolean },
): Promise<PaymentPlan> {
  const { data } = await http.put<UnknownRow>(
    `/payment/plans/${encodeURIComponent(productCode)}/${encodeURIComponent(planCode)}`,
    payload,
  );
  return mapPlan(data);
}
