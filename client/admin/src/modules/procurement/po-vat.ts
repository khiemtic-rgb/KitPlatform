import { formatDisplayMoney } from '@/shared/utils/money';
import type { ProcurementVatTreatment } from '@/shared/api/procurement.types';
import { procurementT } from '@/shared/i18n';

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
  const t = procurementT();
  if (!treatment) return t('shared.tax.selectTreatment');
  if (treatment.isNotSubject) return t('shared.tax.notSubjectPoPreview');
  const tax = computePoTaxAmount(subtotal, treatment);
  if (treatment.ratePercent <= 0) return t('shared.tax.zeroRatePreview');
  return t('shared.tax.taxPreview', {
    tax: formatDisplayMoney(tax),
    rate: treatment.ratePercent,
    subtotal: formatDisplayMoney(subtotal),
  });
}

export function defaultVatTreatmentId(treatments: ProcurementVatTreatment[]): string | undefined {
  return (
    treatments.find((t) => t.treatmentCode === 'vat_8')?.id ??
    treatments.find((t) => !t.isNotSubject && t.ratePercent > 0)?.id ??
    treatments[0]?.id
  );
}
