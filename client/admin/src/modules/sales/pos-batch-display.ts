import type { PosAllocationPreview, PosBatchHint } from '@/shared/api/sales.types';
import { formatDisplayDate } from '@/shared/utils/date';

export function formatBatchHintLine(hint: PosBatchHint): string {
  const expiry = hint.expiryDate ? formatDisplayDate(hint.expiryDate) : '—';
  const qty = hint.quantityAvailable.toLocaleString('vi-VN');
  return `${hint.batchNumber} · HSD ${expiry} · tồn sổ ${qty}`;
}

export function suggestedBatchHint(hints?: PosBatchHint[]): PosBatchHint | undefined {
  return hints?.find((h) => h.isSuggested) ?? hints?.[0];
}

export function formatSuggestedBatch(hints?: PosBatchHint[]): string {
  const hint = suggestedBatchHint(hints);
  if (!hint) return '—';
  const expiry = hint.expiryDate ? formatDisplayDate(hint.expiryDate) : '—';
  return `${hint.batchNumber} (${expiry})`;
}

export function formatAllocationPreviewLine(line: PosAllocationPreview['lines'][number]): string {
  if (line.allocations.length === 0) return '—';
  return line.allocations
    .map((a) => {
      const expiry = a.expiryDate ? formatDisplayDate(a.expiryDate) : '—';
      return `${a.batchNumber} × ${a.quantity.toLocaleString('vi-VN')} (HSD ${expiry})`;
    })
    .join('; ');
}
