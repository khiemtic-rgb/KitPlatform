import { http } from '@/shared/api/http';

type Row = Record<string, unknown>;

export interface PaymentPlan {
  productCode: string;
  planCode: string;
  displayName: string;
  amountVnd: number;
  currency: string;
  intervalDays: number;
}

export interface PaymentMethod {
  providerCode: string;
  displayName: string;
  description: string;
  available: boolean;
  unavailableReason?: string;
}

export interface PaymentSubscription {
  id: string;
  tenantId: string;
  productCode: string;
  subjectType: string;
  subjectId: string;
  planCode: string;
  status: string;
  trialEndsAt?: string;
  currentPeriodEnd?: string;
  autoRenew: boolean;
  isEntitled: boolean;
}

export interface PaymentOrder {
  id: string;
  tenantId: string;
  productCode: string;
  subjectType: string;
  subjectId: string;
  orderCode: number;
  publicCode: string;
  planCode: string;
  amountVnd: number;
  currency: string;
  status: string;
  providerCode?: string;
  checkoutUrl?: string;
  qrCode?: string;
  description?: string;
  paidAt?: string;
  createdAt: string;
  expiresAt?: string;
}

export interface CreatePaymentOrderInput {
  productCode: string;
  subjectType: string;
  subjectId: string;
  planCode?: string;
  returnUrl?: string;
  cancelUrl?: string;
  preferredProvider?: string;
}

function optStr(r: Row, a: string, b: string): string | undefined {
  if (r[a] != null || r[b] != null) return String(r[a] ?? r[b]);
  return undefined;
}

function mapPlan(r: Row): PaymentPlan {
  return {
    productCode: String(r.productCode ?? r.ProductCode ?? ''),
    planCode: String(r.planCode ?? r.PlanCode ?? ''),
    displayName: String(r.displayName ?? r.DisplayName ?? ''),
    amountVnd: Number(r.amountVnd ?? r.AmountVnd ?? 0),
    currency: String(r.currency ?? r.Currency ?? 'VND'),
    intervalDays: Number(r.intervalDays ?? r.IntervalDays ?? 30),
  };
}

function mapMethod(r: Row): PaymentMethod {
  return {
    providerCode: String(r.providerCode ?? r.ProviderCode ?? ''),
    displayName: String(r.displayName ?? r.DisplayName ?? ''),
    description: String(r.description ?? r.Description ?? ''),
    available: Boolean(r.available ?? r.Available ?? false),
    unavailableReason: optStr(r, 'unavailableReason', 'UnavailableReason'),
  };
}

function mapSubscription(r: Row): PaymentSubscription {
  return {
    id: String(r.id ?? r.Id ?? ''),
    tenantId: String(r.tenantId ?? r.TenantId ?? ''),
    productCode: String(r.productCode ?? r.ProductCode ?? ''),
    subjectType: String(r.subjectType ?? r.SubjectType ?? ''),
    subjectId: String(r.subjectId ?? r.SubjectId ?? ''),
    planCode: String(r.planCode ?? r.PlanCode ?? ''),
    status: String(r.status ?? r.Status ?? ''),
    trialEndsAt: optStr(r, 'trialEndsAt', 'TrialEndsAt'),
    currentPeriodEnd: optStr(r, 'currentPeriodEnd', 'CurrentPeriodEnd'),
    autoRenew: Boolean(r.autoRenew ?? r.AutoRenew ?? false),
    isEntitled: Boolean(r.isEntitled ?? r.IsEntitled ?? false),
  };
}

function mapOrder(r: Row): PaymentOrder {
  return {
    id: String(r.id ?? r.Id ?? ''),
    tenantId: String(r.tenantId ?? r.TenantId ?? ''),
    productCode: String(r.productCode ?? r.ProductCode ?? ''),
    subjectType: String(r.subjectType ?? r.SubjectType ?? ''),
    subjectId: String(r.subjectId ?? r.SubjectId ?? ''),
    orderCode: Number(r.orderCode ?? r.OrderCode ?? 0),
    publicCode: String(r.publicCode ?? r.PublicCode ?? ''),
    planCode: String(r.planCode ?? r.PlanCode ?? ''),
    amountVnd: Number(r.amountVnd ?? r.AmountVnd ?? 0),
    currency: String(r.currency ?? r.Currency ?? 'VND'),
    status: String(r.status ?? r.Status ?? ''),
    providerCode: optStr(r, 'providerCode', 'ProviderCode'),
    checkoutUrl: optStr(r, 'checkoutUrl', 'CheckoutUrl'),
    qrCode: optStr(r, 'qrCode', 'QrCode'),
    description: optStr(r, 'description', 'Description'),
    paidAt: optStr(r, 'paidAt', 'PaidAt'),
    createdAt: String(r.createdAt ?? r.CreatedAt ?? ''),
    expiresAt: optStr(r, 'expiresAt', 'ExpiresAt'),
  };
}

function asArray(data: unknown): Row[] {
  if (Array.isArray(data)) return data as Row[];
  if (data && typeof data === 'object') {
    const o = data as Row;
    if (Array.isArray(o.items)) return o.items as Row[];
    if (Array.isArray(o.data)) return o.data as Row[];
  }
  return [];
}

/** Product label for shared checkout shell. */
export function paymentProductLabel(productCode: string): string {
  switch (productCode.trim().toLowerCase()) {
    case 'family_os':
      return 'Famixa';
    case 'novixa':
    case 'pharmacy':
      return 'Novixa';
    case 'kems':
      return 'KEMS';
    default:
      return productCode || 'KIT';
  }
}

export async function listPaymentPlans(productCode: string): Promise<PaymentPlan[]> {
  const { data } = await http.get<unknown>('/payment/plans', {
    params: { productCode },
  });
  return asArray(data).map(mapPlan);
}

export async function listPaymentMethods(): Promise<PaymentMethod[]> {
  const { data } = await http.get<unknown>('/payment/methods');
  return asArray(data).map(mapMethod);
}

export async function fetchPaymentSubscription(input: {
  productCode: string;
  subjectType: string;
  subjectId: string;
}): Promise<PaymentSubscription | null> {
  try {
    const { data } = await http.get<Row>('/payment/subscriptions', { params: input });
    return mapSubscription(data);
  } catch (err: unknown) {
    const status =
      err && typeof err === 'object' && 'response' in err
        ? (err as { response?: { status?: number } }).response?.status
        : undefined;
    if (status === 404) return null;
    throw err;
  }
}

export async function createPaymentOrder(
  input: CreatePaymentOrderInput,
): Promise<PaymentOrder> {
  const { data } = await http.post<Row>('/payment/orders', {
    productCode: input.productCode,
    subjectType: input.subjectType,
    subjectId: input.subjectId,
    planCode: input.planCode ?? null,
    returnUrl: input.returnUrl ?? null,
    cancelUrl: input.cancelUrl ?? null,
    preferredProvider: input.preferredProvider ?? null,
  });
  return mapOrder(data);
}

export async function getPaymentOrder(input: {
  orderCode: number;
  productCode: string;
  subjectId: string;
}): Promise<PaymentOrder> {
  const { data } = await http.get<Row>(`/payment/orders/${input.orderCode}`, {
    params: {
      productCode: input.productCode,
      subjectId: input.subjectId,
    },
  });
  return mapOrder(data);
}

export function buildCheckoutPath(input: {
  productCode: string;
  subjectType: string;
  subjectId: string;
  planCode?: string;
  returnPath?: string;
  orderCode?: number;
}): string {
  const q = new URLSearchParams({
    product: input.productCode,
    subjectType: input.subjectType,
    subjectId: input.subjectId,
  });
  if (input.planCode) q.set('plan', input.planCode);
  if (input.returnPath) q.set('return', input.returnPath);
  if (input.orderCode != null && input.orderCode > 0) {
    q.set('orderCode', String(input.orderCode));
  }
  return `/pay?${q.toString()}`;
}
