export type RxEnforcementModeValue = 'off' | 'strict' | 'warn';

export type TenantRxSettings = {
  enforcementMode: RxEnforcementModeValue;
  posBlockedAudit: boolean;
};

export function requiresPrescription(dispensingClass?: string | null): boolean {
  const value = (dispensingClass ?? 'otc').toLowerCase();
  return value === 'prescription' || value === 'controlled';
}

export function shouldBlockRxAtPos(
  dispensingClass: string | undefined | null,
  enforcementMode: RxEnforcementModeValue,
  options?: { hasVerifiedPrescription?: boolean },
): boolean {
  // Đơn BS Rx module hoặc đơn Connect từ PK đều đủ ngữ cảnh kê đơn
  if (options?.hasVerifiedPrescription) return false;
  return enforcementMode === 'strict' && requiresPrescription(dispensingClass);
}

export const RX_POS_BLOCK_MESSAGE =
  'Thuốc kê đơn — cần nạp đơn bác sĩ (Bán theo đơn phòng khám / Connect) trước khi bán.';

export function normalizeRxSettings(data: Record<string, unknown>): TenantRxSettings {
  const mode = String(data.enforcementMode ?? data.EnforcementMode ?? 'off').toLowerCase();
  return {
    enforcementMode: mode === 'strict' || mode === 'warn' ? mode : 'off',
    posBlockedAudit: Boolean(data.posBlockedAudit ?? data.PosBlockedAudit ?? true),
  };
}
