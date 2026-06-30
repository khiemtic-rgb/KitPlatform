import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

const CUSTOMER_STATUS_IDS = [0, 1] as const;
const CUSTOMER_GENDER_IDS = [1, 2] as const;
const LOYALTY_TX_IDS = [1, 2, 3, 4] as const;

export function useCustomerEnums() {
  const { t } = useTranslation('customer');

  const customerStatusLabel = (status: number) =>
    t(`enums.customerStatus.${status}`, { defaultValue: String(status) });

  const customerGenderLabel = (gender: number) =>
    t(`enums.customerGender.${gender}`, { defaultValue: String(gender) });

  const loyaltyTxLabel = (type: number) =>
    t(`enums.loyaltyTx.${type}`, { defaultValue: String(type) });

  const customerStatusOptions = useMemo(
    () => CUSTOMER_STATUS_IDS.map((value) => ({ value, label: customerStatusLabel(value) })),
    [t],
  );

  const customerGenderOptions = useMemo(
    () => CUSTOMER_GENDER_IDS.map((value) => ({ value, label: customerGenderLabel(value) })),
    [t],
  );

  const loyaltyTxOptions = useMemo(
    () => LOYALTY_TX_IDS.map((value) => ({ value, label: loyaltyTxLabel(value) })),
    [t],
  );

  return {
    customerStatusLabel,
    customerStatusOptions,
    customerGenderLabel,
    customerGenderOptions,
    loyaltyTxLabel,
    loyaltyTxOptions,
  };
}
