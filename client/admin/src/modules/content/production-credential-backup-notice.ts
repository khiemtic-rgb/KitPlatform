export const PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED = 'PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED';

export type ProductionCredentialBackupGate = {
  writeGate: string;
  restoreVerification: string;
  encryptionKeyId: string | null;
  blockCode: string | null;
};

export type BackupGateLoadError = 'forbidden' | 'unavailable';

export const BACKUP_BLOCKED_TITLE = 'Chưa thể lưu thông tin xác thực';

export const BACKUP_BLOCKED_BODY =
  'Hệ thống chưa xác nhận điều kiện sao lưu an toàn cho khóa mã hóa Production. API Key chưa được lưu. Quản trị viên cần hoàn tất quy trình xác minh backup/khôi phục khóa mã hóa trước khi thử lại.';

export function productionCredentialErrorCode(error: unknown): string | null {
  const data = (error as { response?: { data?: unknown } } | null)?.response?.data;
  if (!data || typeof data !== 'object') return null;
  const record = data as { code?: unknown; message?: unknown };
  if (typeof record.code === 'string' && record.code.startsWith('PRODUCTION_CREDENTIAL_')) return record.code;
  if (typeof record.message === 'string' && record.message.startsWith('PRODUCTION_CREDENTIAL_')) return record.message;
  return null;
}

/** Restore proof is never inferred. OPEN still means the restore check is unverified. */
export function backupConditionLabel(
  gate: ProductionCredentialBackupGate | null,
  loadError: BackupGateLoadError | null,
): 'Đạt' | 'Chưa xác minh' | 'Chưa đạt' | 'Không kiểm tra được' | null {
  if (loadError) return 'Không kiểm tra được';
  if (!gate) return null;
  if (gate.restoreVerification === 'VERIFIED' && gate.writeGate === 'OPEN') return 'Đạt';
  if (gate.restoreVerification !== 'UNVERIFIED') return 'Không kiểm tra được';
  if (gate.writeGate === 'BLOCKED') return 'Chưa đạt';
  if (gate.writeGate === 'OPEN') return 'Chưa xác minh';
  return 'Không kiểm tra được';
}

