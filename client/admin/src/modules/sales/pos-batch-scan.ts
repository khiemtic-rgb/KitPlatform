import type { CartLine } from '@/shared/api/sales.types';
import { batchLabelMatchesHints } from '@/modules/sales/pos-batch-mode-ui';

function resolveBatchNumber(scan: string, line: CartLine): string | null {
  const normalized = scan.trim();
  if (!normalized) return null;
  if (!batchLabelMatchesHints(normalized, line.batchHints)) return null;
  const match = line.batchHints?.find(
    (h) => h.batchNumber.trim().toLowerCase() === normalized.toLowerCase(),
  );
  return match?.batchNumber ?? normalized;
}

/**
 * Gán số lô từ quét mã vạch nhãn lô vào dòng giỏ phù hợp.
 * Ưu tiên dòng chưa có lô, khớp batchHints; nếu không có thì dòng khớp cuối cùng.
 */
export function applyBatchLabelScan(
  cart: CartLine[],
  scan: string,
): { cart: CartLine[]; batchNumber: string; productName: string } | null {
  const normalized = scan.trim();
  if (!normalized || cart.length === 0) return null;

  const matches = cart
    .map((line, index) => ({ line, index }))
    .filter(({ line }) => resolveBatchNumber(normalized, line) !== null);

  if (matches.length === 0) return null;

  const pending = matches.filter(({ line }) => !line.batchLabel?.trim());
  const target = (pending.length > 0 ? pending[pending.length - 1] : matches[matches.length - 1]).line;
  const batchNumber = resolveBatchNumber(normalized, target);
  if (!batchNumber) return null;

  return {
    cart: cart.map((line) =>
      line.key === target.key ? { ...line, batchLabel: batchNumber } : line,
    ),
    batchNumber,
    productName: target.productName,
  };
}
