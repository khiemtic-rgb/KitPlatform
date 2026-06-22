import type { ProductUnit } from '@/shared/api/catalog.types';

export function pickDefaultProductUnitId(units: ProductUnit[]): string | undefined {
  if (units.length === 0) return undefined;
  return (
    units.find((u) => u.isBaseUnit)?.id ??
    units.find((u) => u.isSaleUnit)?.id ??
    units[0]?.id
  );
}

export function formatUnitLabel(unit: ProductUnit): string {
  if (unit.conversionFactor > 1) {
    return `${unit.unitName} (×${unit.conversionFactor})`;
  }
  return unit.unitName;
}
