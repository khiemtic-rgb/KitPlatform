/** FEFO + stock helpers for inventory-count batch picker. */

export type ExpiryTone = 'ok' | 'near' | 'expired' | 'unknown';

export const NEAR_EXPIRY_DAYS = 90;

export function startOfLocalDay(d = new Date()): Date {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  return x;
}

export function daysUntilExpiry(expiryDate?: string | null, from = startOfLocalDay()): number | null {
  if (!expiryDate) return null;
  const exp = Date.parse(expiryDate);
  if (Number.isNaN(exp)) return null;
  const end = startOfLocalDay(new Date(exp));
  return Math.round((end.getTime() - from.getTime()) / 86_400_000);
}

export function getExpiryTone(expiryDate?: string | null, nearDays = NEAR_EXPIRY_DAYS): ExpiryTone {
  const days = daysUntilExpiry(expiryDate);
  if (days == null) return 'unknown';
  if (days < 0) return 'expired';
  if (days <= nearDays) return 'near';
  return 'ok';
}

export function expiryToneColor(tone: ExpiryTone): string | undefined {
  if (tone === 'expired') return '#cf1322';
  if (tone === 'near') return '#d48806';
  return undefined;
}

export type CountBatchSortable = {
  batchNumber: string;
  expiryDate?: string | null;
  quantityAvailable: number;
};

/** Tồn > 0 trước, rồi FEFO (HSD sớm trước), rồi mã lô. */
export function sortBatchesForCount<T extends CountBatchSortable>(batches: T[]): T[] {
  return [...batches].sort((a, b) => {
    const aZero = a.quantityAvailable <= 0 ? 1 : 0;
    const bZero = b.quantityAvailable <= 0 ? 1 : 0;
    if (aZero !== bZero) return aZero - bZero;

    const ae = a.expiryDate ? Date.parse(a.expiryDate) : Number.POSITIVE_INFINITY;
    const be = b.expiryDate ? Date.parse(b.expiryDate) : Number.POSITIVE_INFINITY;
    const aMissing = Number.isNaN(ae);
    const bMissing = Number.isNaN(be);
    if (aMissing !== bMissing) return aMissing ? 1 : -1;
    if (ae !== be) return ae - be;

    return a.batchNumber.localeCompare(b.batchNumber, 'vi');
  });
}
