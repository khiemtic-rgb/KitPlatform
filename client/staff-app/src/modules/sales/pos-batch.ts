import type { CartLine, TenantBatchModeValue } from '@/shared/api/sales.types';

export function suggestedBatchHint(hints?: CartLine['batchHints']) {
  return hints?.find((h) => h.isSuggested) ?? hints?.[0];
}

export function defaultBatchLabel(hints?: CartLine['batchHints']): string | undefined {
  return suggestedBatchHint(hints)?.batchNumber;
}

function formatHintMonthYear(expiryDate?: string): string {
  if (!expiryDate) return '';
  const [year, month] = expiryDate.slice(0, 10).split('-');
  if (!year || !month) return '';
  return ` · HSD ${month}/${year}`;
}

export function formatCartLotButton(line: Pick<CartLine, 'batchLabel' | 'batchHints'>): string {
  const label = line.batchLabel || defaultBatchLabel(line.batchHints);
  const hint =
    (line.batchHints ?? []).find((h) => h.batchNumber === label) ?? suggestedBatchHint(line.batchHints);
  const fefo = hint?.isSuggested ? ' · FEFO' : '';
  return `Lô: ${label || 'Chọn lô'}${formatHintMonthYear(hint?.expiryDate)}${fefo}`;
}

export function showsBatchHints(mode: TenantBatchModeValue): boolean {
  return mode === 'suggest';
}

export function showsBatchLabelField(mode: TenantBatchModeValue): boolean {
  return mode === 'suggest' || mode === 'label_optional' || mode === 'label_required';
}

export function showsBatchPicker(mode: TenantBatchModeValue, hints?: CartLine['batchHints']): boolean {
  if (mode === 'off' || !hints?.length) return false;
  return showsBatchLabelField(mode);
}

export function requiresBatchLabel(mode: TenantBatchModeValue): boolean {
  return mode === 'label_required';
}

export function batchLabelMatchesHints(label: string, hints?: CartLine['batchHints']): boolean {
  const normalized = label.trim().toLowerCase();
  if (!normalized) return false;
  return hints?.some((h) => h.batchNumber.trim().toLowerCase() === normalized) ?? false;
}

export function validateCartBatchLabels(cart: CartLine[], mode: TenantBatchModeValue): string | null {
  if (mode === 'off') return null;

  for (const line of cart) {
    const label = line.batchLabel?.trim() ?? '';
    if (requiresBatchLabel(mode) && !label) {
      return `Chọn lô cho "${line.productName}"`;
    }
    if (label && !batchLabelMatchesHints(label, line.batchHints)) {
      return `Lô "${label}" không khớp tồn kho — "${line.productName}"`;
    }
  }

  return null;
}
