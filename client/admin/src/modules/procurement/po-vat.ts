import { formatDisplayMoney } from '@/shared/utils/money';
import type { ProcurementVatTreatment } from '@/shared/api/procurement.types';

export function computePoTaxAmount(
  subtotal: number,
  treatment: Pick<ProcurementVatTreatment, 'isNotSubject' | 'ratePercent'> | undefined,
): number {
  if (!treatment || treatment.isNotSubject || treatment.ratePercent <= 0) return 0;
  return Math.round(((subtotal * treatment.ratePercent) / 100) * 100) / 100;
}

export function formatVatTreatmentOptionLabel(treatment: ProcurementVatTreatment): string {
  return treatment.treatmentName;
}

export function formatPoTaxPreview(
  subtotal: number,
  treatment: ProcurementVatTreatment | undefined,
): string {
  if (!treatment) return 'Chọn loại thuế';
  if (treatment.isNotSubject) return 'Không chịu thuế GTGT — không tính VAT trên PO';
  const tax = computePoTaxAmount(subtotal, treatment);
  if (treatment.ratePercent <= 0) return 'Thuế suất 0% — không phát sinh tiền thuế';
  return `≈ ${formatDisplayMoney(tax)} (${treatment.ratePercent}% × ${formatDisplayMoney(subtotal)})`;
}

export function defaultVatTreatmentId(treatments: ProcurementVatTreatment[]): string | undefined {
  return (
    treatments.find((t) => t.treatmentCode === 'vat_8')?.id ??
    treatments.find((t) => !t.isNotSubject && t.ratePercent > 0)?.id ??
    treatments[0]?.id
  );
}
