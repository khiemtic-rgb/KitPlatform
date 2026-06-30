import type { PosAllocationPreview, PosBatchHint } from '@/shared/api/sales.types';
import { salesT } from '@/shared/i18n';
import { formatDisplayDate } from '@/shared/utils/date';

export function formatBatchHintLine(hint: PosBatchHint): string {
  const t = salesT();
  const expiry = hint.expiryDate ? formatDisplayDate(hint.expiryDate) : '—';
  const qty = hint.quantityAvailable.toLocaleString();
  return t('pos.batch.hintLine', { batch: hint.batchNumber, expiry, qty });
}

export function suggestedBatchHint(hints?: PosBatchHint[]): PosBatchHint | undefined {
  return hints?.find((h) => h.isSuggested) ?? hints?.[0];
}

export function formatSuggestedBatch(hints?: PosBatchHint[]): string {
  const hint = suggestedBatchHint(hints);
  if (!hint) return '—';
  const t = salesT();
  const expiry = hint.expiryDate ? formatDisplayDate(hint.expiryDate) : '—';
  return t('pos.batch.suggestedBatch', { batch: hint.batchNumber, expiry });
}

export function formatAllocationPreviewLine(line: PosAllocationPreview['lines'][number]): string {
  if (line.allocations.length === 0) return '—';
  const t = salesT();
  return line.allocations
    .map((a) => {
      const expiry = a.expiryDate ? formatDisplayDate(a.expiryDate) : '—';
      return t('pos.batch.allocationLine', {
        batch: a.batchNumber,
        qty: a.quantity.toLocaleString(),
        expiry,
      });
    })
    .join('; ');
}
