import { http } from '@/shared/api/http';

export type TenantCsdlDuocLink = {
  enabled: boolean;
  environment: string;
  username?: string;
  passwordConfigured: boolean;
  practiceLicenseCode?: string;
  enableStockOutSync: boolean;
  enableStockInSync: boolean;
  status: string;
  lastCheckAt?: string;
  lastError?: string;
  connectedAt?: string;
  activeAccountSource: string;
  activeAccountUsername?: string;
  activeAccountLabel?: string;
};

export type UpdateTenantCsdlDuocLinkRequest = {
  enabled: boolean;
  environment: string;
  username?: string | null;
  /** null = keep existing password */
  password?: string | null;
  practiceLicenseCode?: string | null;
  enableStockOutSync: boolean;
  enableStockInSync: boolean;
};

function asString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function normalizeLink(row: Record<string, unknown>): TenantCsdlDuocLink {
  return {
    enabled: Boolean(row.enabled ?? row.Enabled),
    environment: String(row.environment ?? row.Environment ?? 'sandbox'),
    username: asString(row.username ?? row.Username),
    passwordConfigured: Boolean(row.passwordConfigured ?? row.PasswordConfigured),
    practiceLicenseCode: asString(row.practiceLicenseCode ?? row.PracticeLicenseCode),
    enableStockOutSync: Boolean(row.enableStockOutSync ?? row.EnableStockOutSync),
    enableStockInSync: Boolean(row.enableStockInSync ?? row.EnableStockInSync),
    status: String(row.status ?? row.Status ?? 'NotConfigured'),
    lastCheckAt: asString(row.lastCheckAt ?? row.LastCheckAt),
    lastError: asString(row.lastError ?? row.LastError),
    connectedAt: asString(row.connectedAt ?? row.ConnectedAt),
    activeAccountSource: String(row.activeAccountSource ?? row.ActiveAccountSource ?? 'platform'),
    activeAccountUsername: asString(row.activeAccountUsername ?? row.ActiveAccountUsername),
    activeAccountLabel: asString(row.activeAccountLabel ?? row.ActiveAccountLabel),
  };
}

export async function fetchTenantCsdlDuocLink(): Promise<TenantCsdlDuocLink> {
  const { data } = await http.get<Record<string, unknown>>('/pharmacy/integration/csdl-duoc/link');
  return normalizeLink(data);
}

export async function updateTenantCsdlDuocLink(
  request: UpdateTenantCsdlDuocLinkRequest,
): Promise<TenantCsdlDuocLink> {
  const { data } = await http.put<Record<string, unknown>>(
    '/pharmacy/integration/csdl-duoc/link',
    request,
  );
  return normalizeLink(data);
}

export async function testTenantCsdlDuocLink(): Promise<TenantCsdlDuocLink> {
  const { data } = await http.post<Record<string, unknown>>(
    '/pharmacy/integration/csdl-duoc/link/test',
  );
  return normalizeLink(data);
}
