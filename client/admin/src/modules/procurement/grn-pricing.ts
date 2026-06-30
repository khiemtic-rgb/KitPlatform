import { computePoTaxAmount } from '@/modules/procurement/po-vat';
import type { ProcurementVatTreatment } from '@/shared/api/procurement.types';
import { procurementT } from '@/shared/i18n';
import { formatDisplayMoney } from '@/shared/utils/money';

export const PROCUREMENT_DISCOUNT_TYPES = {
  Percent: 1,
  Fixed: 2,
} as const;

export type ProcurementDiscountType =
  (typeof PROCUREMENT_DISCOUNT_TYPES)[keyof typeof PROCUREMENT_DISCOUNT_TYPES];

export interface GrnLinePricingLike {
  quantity?: number;
  unitCost?: number;
  discountType?: ProcurementDiscountType;
  discountValue?: number;
}

export interface GrnOrderDiscountState {
  discountType?: ProcurementDiscountType;
  discountValue?: number;
}

function computeDiscountAmount(
  basis: number,
  discountType?: ProcurementDiscountType,
  discountValue?: number,
): number {
  if (basis <= 0 || !discountType || !discountValue || discountValue <= 0) return 0;
  if (discountType === PROCUREMENT_DISCOUNT_TYPES.Percent) {
    return Math.round((basis * Math.min(discountValue, 100)) / 100);
  }
  return Math.min(Math.round(discountValue), Math.round(basis));
}

export function grnLineGross(line: GrnLinePricingLike | undefined): number {
  return Math.round((line?.quantity ?? 0) * (line?.unitCost ?? 0));
}

export function grnLineDiscountAmount(line: GrnLinePricingLike | undefined): number {
  return computeDiscountAmount(grnLineGross(line), line?.discountType, line?.discountValue);
}

export function grnLineNetTotal(line: GrnLinePricingLike | undefined): number {
  return grnLineGross(line) - grnLineDiscountAmount(line);
}

export function computeGrnPricing(
  items: GrnLinePricingLike[] | undefined,
  orderDiscount: GrnOrderDiscountState,
  vatTreatment?: Pick<ProcurementVatTreatment, 'isNotSubject' | 'ratePercent'> | null,
) {
  const subtotalGross = (items ?? []).reduce((sum, line) => sum + grnLineGross(line), 0);
  const lineDiscountTotal = (items ?? []).reduce((sum, line) => sum + grnLineDiscountAmount(line), 0);
  const merchandiseNet = subtotalGross - lineDiscountTotal;
  const orderDiscountAmount = computeDiscountAmount(
    merchandiseNet,
    orderDiscount.discountType,
    orderDiscount.discountValue,
  );
  const taxableAmount = merchandiseNet - orderDiscountAmount;
  const taxAmount =
    vatTreatment && !vatTreatment.isNotSubject && vatTreatment.ratePercent > 0
      ? computePoTaxAmount(taxableAmount, vatTreatment)
      : 0;
  const totalAmount = taxableAmount + taxAmount;

  return {
    subtotalGross,
    lineDiscountTotal,
    merchandiseNet,
    orderDiscountAmount,
    taxAmount,
    totalAmount,
  };
}

export function isPlaceholderSupplier(supplier: { supplierCode?: string; isPlaceholder?: boolean }) {
  return Boolean(supplier.isPlaceholder || supplier.supplierCode === 'NCC-TBD');
}

export function realSuppliers<T extends { supplierCode?: string; isPlaceholder?: boolean }>(suppliers: T[]) {
  return suppliers.filter((s) => !isPlaceholderSupplier(s));
}

export function formatGrnDiscountDisplay(
  discountType?: ProcurementDiscountType,
  discountValue?: number,
  discountAmount?: number,
): string {
  if (!discountType || !discountValue || discountValue <= 0) {
    if (discountAmount != null && discountAmount > 0) {
      return `−${formatDisplayMoney(discountAmount)}`;
    }
    return '—';
  }
  if (discountType === PROCUREMENT_DISCOUNT_TYPES.Percent) {
    const amountSuffix =
      discountAmount != null && discountAmount > 0 ? ` (−${formatDisplayMoney(discountAmount)})` : '';
    return `${discountValue}%${amountSuffix}`;
  }
  return `−${formatDisplayMoney(discountValue)}`;
}

export function formatGrnLineDiscountCompact(
  discountType?: ProcurementDiscountType,
  discountValue?: number,
  discountAmount?: number,
): string {
  if (!discountType || !discountValue || discountValue <= 0) {
    if (discountAmount != null && discountAmount > 0) {
      return `−${formatDisplayMoney(discountAmount)}`;
    }
    return '—';
  }
  if (discountType === PROCUREMENT_DISCOUNT_TYPES.Percent) {
    return `${discountValue}%`;
  }
  return `−${formatDisplayMoney(discountValue)}`;
}

export function formatGrnVatLabel(detail: {
  vatTreatmentName?: string;
  vatIsNotSubject?: boolean;
  taxRatePercent?: number;
}): string {
  const t = procurementT();
  if (detail.vatIsNotSubject) return t('shared.tax.notSubject');
  if (detail.vatTreatmentName) {
    const rate = detail.taxRatePercent ?? 0;
    return rate > 0
      ? t('shared.tax.treatmentWithRate', { name: detail.vatTreatmentName, rate })
      : detail.vatTreatmentName;
  }
  return t('shared.emDash');
}
