import type { CartLine } from '@/shared/api/sales.types';
import { SALES_DISCOUNT_TYPES } from '@/shared/api/sales.types';

export const POS_LINE_ADJUST = {
  Percent: 'percent',
  Fixed: 'fixed',
  UnitPrice: 'unit_price',
} as const;

export type PosLineAdjust = (typeof POS_LINE_ADJUST)[keyof typeof POS_LINE_ADJUST];

export function catalogPriceOf(line: CartLine): number {
  return line.catalogUnitPrice ?? line.unitPrice;
}

export function resolveLineAdjust(line: CartLine): PosLineAdjust | undefined {
  if (line.lineAdjust) return line.lineAdjust;
  if (line.discountType === SALES_DISCOUNT_TYPES.Percent) return POS_LINE_ADJUST.Percent;
  if (line.discountType === SALES_DISCOUNT_TYPES.Fixed) return POS_LINE_ADJUST.Fixed;
  const catalog = catalogPriceOf(line);
  if (Math.abs(line.unitPrice - catalog) > 0.009) return POS_LINE_ADJUST.UnitPrice;
  return undefined;
}

export function applyLineAdjustClear(line: CartLine): CartLine {
  return {
    ...line,
    lineAdjust: undefined,
    discountType: undefined,
    discountValue: undefined,
    unitPrice: catalogPriceOf(line),
  };
}

export function applyLineAdjustMode(line: CartLine, mode: PosLineAdjust | undefined): CartLine {
  if (!mode) return applyLineAdjustClear(line);

  if (mode === POS_LINE_ADJUST.UnitPrice) {
    return {
      ...line,
      lineAdjust: POS_LINE_ADJUST.UnitPrice,
      discountType: undefined,
      discountValue: undefined,
    };
  }

  return {
    ...line,
    unitPrice: catalogPriceOf(line),
    lineAdjust: mode,
    discountType:
      mode === POS_LINE_ADJUST.Percent ? SALES_DISCOUNT_TYPES.Percent : SALES_DISCOUNT_TYPES.Fixed,
    discountValue: line.discountValue ?? 0,
  };
}

export function applyLineAdjustValue(line: CartLine, value: number): CartLine {
  const mode = resolveLineAdjust(line);
  if (mode === POS_LINE_ADJUST.UnitPrice) {
    return {
      ...line,
      lineAdjust: POS_LINE_ADJUST.UnitPrice,
      unitPrice: value,
      discountType: undefined,
      discountValue: undefined,
    };
  }
  if (mode === POS_LINE_ADJUST.Percent || mode === POS_LINE_ADJUST.Fixed) {
    return { ...line, discountValue: value };
  }
  return line;
}
