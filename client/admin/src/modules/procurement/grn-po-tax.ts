import type { PurchaseOrderDetail } from '@/shared/api/procurement.types';
import { computePoTaxAmount } from '@/modules/procurement/po-vat';

export interface GrnLineCostLike {
  quantity?: number;
  unitCost?: number;
}

export function sumGrnSubtotal(items: GrnLineCostLike[] | undefined): number {
  if (!items?.length) return 0;
  return items.reduce((sum, line) => sum + grnLineTotal(line), 0);
}

export function grnLineTotal(line: GrnLineCostLike | undefined): number {
  return Math.round((line?.quantity ?? 0) * (line?.unitCost ?? 0) * 100) / 100;
}

function poVatRatePercent(po: PurchaseOrderDetail): number {
  if (po.vatIsNotSubject) return 0;
  if (po.taxRatePercent != null && po.taxRatePercent > 0) return po.taxRatePercent;
  if (po.subtotal > 0 && po.taxAmount > 0) {
    return Math.round((po.taxAmount / po.subtotal) * 10000) / 100;
  }
  return 0;
}

export function computeGrnTaxFromPo(
  po: PurchaseOrderDetail,
  items: GrnLineCostLike[] | undefined,
): { subtotal: number; taxAmount: number; totalAmount: number } {
  const subtotal = Math.round(sumGrnSubtotal(items) * 100) / 100;
  const taxAmount = computePoTaxAmount(subtotal, {
    isNotSubject: po.vatIsNotSubject,
    ratePercent: poVatRatePercent(po),
  });
  return {
    subtotal,
    taxAmount,
    totalAmount: Math.round((subtotal + taxAmount) * 100) / 100,
  };
}

export function computeGrnTaxTotals(
  items: GrnLineCostLike[] | undefined,
  linkedPo?: PurchaseOrderDetail | null,
): { subtotal: number; taxAmount: number; totalAmount: number } {
  if (linkedPo) return computeGrnTaxFromPo(linkedPo, items);
  const subtotal = Math.round(sumGrnSubtotal(items) * 100) / 100;
  return { subtotal, taxAmount: 0, totalAmount: subtotal };
}
