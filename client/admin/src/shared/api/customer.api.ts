import { http } from '@/shared/api/http';

export interface CustomerConsent {
  id: string;
  customerId: string;
  channel: number;
  purpose: number;
  granted: boolean;
  grantedAt?: string;
  revokedAt?: string;
  source: number;
  notes?: string;
}

export const CONSENT_CHANNEL_LABELS: Record<number, string> = {
  1: 'SMS',
  2: 'Zalo',
  3: 'Email',
  4: 'App push',
};

export const CONSENT_PURPOSE_LABELS: Record<number, string> = {
  1: 'Marketing',
  2: 'Nhắc chăm sóc',
  3: 'Nghiên cứu',
  4: 'Hỗ trợ AI dược sĩ',
};

function normalizeConsent(row: Record<string, unknown>): CustomerConsent {
  return {
    id: String(row.id ?? row.Id),
    customerId: String(row.customerId ?? row.CustomerId),
    channel: Number(row.channel ?? row.Channel ?? 1),
    purpose: Number(row.purpose ?? row.Purpose ?? 1),
    granted: Boolean(row.granted ?? row.Granted),
    grantedAt: (row.grantedAt ?? row.GrantedAt) as string | undefined,
    revokedAt: (row.revokedAt ?? row.RevokedAt) as string | undefined,
    source: Number(row.source ?? row.Source ?? 2),
    notes: (row.notes ?? row.Notes) as string | undefined,
  };
}

export async function fetchCustomerConsents(customerId: string): Promise<CustomerConsent[]> {
  const { data } = await http.get<Record<string, unknown>[]>(`/customers/${customerId}/consents`);
  return data.map(normalizeConsent);
}

export async function upsertCustomerConsents(
  customerId: string,
  items: { channel: number; purpose: number; granted: boolean; source?: number; notes?: string }[],
): Promise<CustomerConsent[]> {
  const { data } = await http.put<Record<string, unknown>[]>(`/customers/${customerId}/consents`, {
    items,
  });
  return data.map(normalizeConsent);
}
