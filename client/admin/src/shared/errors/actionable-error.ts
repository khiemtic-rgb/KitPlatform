export type ErrorCategory =
  | 'user_input'
  | 'configuration'
  | 'permission'
  | 'dependency'
  | 'security_gate'
  | 'conflict'
  | 'transient'
  | 'operational'
  | 'unknown';

export type RegisteredRecovery = {
  code: string;
  message: string;
  category: ErrorCategory;
  actionType: string;
  actionLabel: string;
  actionTarget: string;
  route: string;
  retryable: boolean;
};

const RECOVERY: Record<string, RegisteredRecovery> = {
  PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED: {
    code: 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED',
    message: 'Chưa đủ điều kiện bảo mật để lưu thông tin xác thực. Nội dung vừa nhập chưa được lưu.',
    category: 'security_gate',
    actionType: 'open_security_condition',
    actionLabel: 'Mở điều kiện bảo mật',
    actionTarget: 'content.productionCredentialSecurity',
    route: '/content/ai/credential-security',
    retryable: true,
  },
};

const ROUTES: Record<string, string> = {
  'content.productionCredentialSecurity': '/content/ai/credential-security',
};

export function registeredRecoveryRoute(actionTarget: unknown): string | null {
  if (typeof actionTarget !== 'string' || actionTarget.length === 0) return null;
  if (actionTarget.includes('://') || actionTarget.startsWith('/') || actionTarget.includes('..')) return null;
  return ROUTES[actionTarget] ?? null;
}

export function recoveryForCode(code: string | null | undefined): RegisteredRecovery | null {
  if (!code) return null;
  return RECOVERY[code] ?? null;
}

export function readActionableRecovery(error: unknown): RegisteredRecovery | null {
  const data = (error as { response?: { data?: unknown } } | null)?.response?.data;
  const record = data && typeof data === 'object' ? (data as { code?: unknown; message?: unknown }) : null;
  const code =
    typeof record?.code === 'string'
      ? record.code
      : typeof record?.message === 'string' && record.message.startsWith('PRODUCTION_CREDENTIAL_')
        ? record.message
        : null;
  return recoveryForCode(code);
}
