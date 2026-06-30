import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

const PO_STATUS_IDS = [1, 2, 3, 4, 5, 6] as const;
const GRN_STATUS_IDS = [1, 2, 3] as const;
const SUPPLIER_STATUS_IDS = [1, 2] as const;
const PAYMENT_METHOD_IDS = [1, 2, 3] as const;
const SUPPLIER_PAYMENT_STATUS_IDS = [1, 2, 3] as const;

export function useProcurementEnums() {
  const { t } = useTranslation('procurement');

  const poStatusLabel = (status: number) =>
    t(`enums.poStatus.${status}`, { defaultValue: String(status) });

  const grnStatusLabel = (status: number) =>
    t(`enums.grnStatus.${status}`, { defaultValue: String(status) });

  const supplierStatusLabel = (status: number) =>
    t(`enums.supplierStatus.${status}`, { defaultValue: String(status) });

  const paymentMethodLabel = (method: number) =>
    t(`enums.paymentMethod.${method}`, { defaultValue: String(method) });

  const supplierPaymentStatusLabel = (status: number) =>
    t(`enums.supplierPaymentStatus.${status}`, { defaultValue: String(status) });

  const poStatusOptions = useMemo(
    () => PO_STATUS_IDS.map((value) => ({ value, label: poStatusLabel(value) })),
    [t],
  );

  const grnStatusOptions = useMemo(
    () => GRN_STATUS_IDS.map((value) => ({ value, label: grnStatusLabel(value) })),
    [t],
  );

  const supplierStatusOptions = useMemo(
    () => SUPPLIER_STATUS_IDS.map((value) => ({ value, label: supplierStatusLabel(value) })),
    [t],
  );

  const paymentMethodOptions = useMemo(
    () => PAYMENT_METHOD_IDS.map((value) => ({ value, label: paymentMethodLabel(value) })),
    [t],
  );

  const supplierPaymentStatusOptions = useMemo(
    () => SUPPLIER_PAYMENT_STATUS_IDS.map((value) => ({ value, label: supplierPaymentStatusLabel(value) })),
    [t],
  );

  return {
    poStatusLabel,
    poStatusOptions,
    grnStatusLabel,
    grnStatusOptions,
    supplierStatusLabel,
    supplierStatusOptions,
    paymentMethodLabel,
    paymentMethodOptions,
    supplierPaymentStatusLabel,
    supplierPaymentStatusOptions,
  };
}
