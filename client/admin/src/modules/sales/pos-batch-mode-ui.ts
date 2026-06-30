import type { TFunction } from 'i18next';
import type { TenantBatchModeValue } from '@/shared/api/sales.api';
import type { CartLine, PosBatchHint } from '@/shared/api/sales.types';
import { suggestedBatchHint } from '@/modules/sales/pos-batch-display';

export function showsBatchHints(mode: TenantBatchModeValue): boolean {
  return mode === 'suggest';
}

export function showsBatchLabelField(mode: TenantBatchModeValue): boolean {
  return mode === 'label_optional' || mode === 'label_required';
}

export function requiresBatchLabel(mode: TenantBatchModeValue): boolean {
  return mode === 'label_required';
}

export function defaultBatchLabel(hints?: PosBatchHint[]): string | undefined {
  return suggestedBatchHint(hints)?.batchNumber;
}

export function batchLabelMatchesHints(label: string, hints?: PosBatchHint[]): boolean {
  const normalized = label.trim().toLowerCase();
  if (!normalized) return false;
  return hints?.some((h) => h.batchNumber.trim().toLowerCase() === normalized) ?? false;
}

export function initialBatchLabelForMode(
  _mode: TenantBatchModeValue,
  hints?: PosBatchHint[],
): string | undefined {
  return defaultBatchLabel(hints);
}

export function validateCartBatchLabels(
  cart: CartLine[],
  mode: TenantBatchModeValue,
  t?: TFunction<'sales'>,
): string | null {
  if (!showsBatchLabelField(mode)) return null;

  for (const line of cart) {
    const label = line.batchLabel?.trim() ?? '';
    if (!label) continue;
    if (!batchLabelMatchesHints(label, line.batchHints)) {
      if (t) {
        return t('pos.batch.labelMismatch', { label, product: line.productName });
      }
      return `Số lô "${label}" không khớp tồn kho cho "${line.productName}"`;
    }
  }

  return null;
}
