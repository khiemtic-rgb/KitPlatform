import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

const DRUG_TYPE_IDS = [1, 2, 3] as const;
const PRICE_TYPE_IDS = [1, 2, 3, 4, 5] as const;
const BARCODE_TYPE_IDS = [1, 2, 3, 4] as const;
const PRODUCT_STATUS_IDS = [1, 2] as const;

export function useCatalogEnums() {
  const { t } = useTranslation('catalog');

  const drugTypeLabel = (type: number) =>
    t(`enums.drugType.${type}`, { defaultValue: String(type) });

  const priceTypeLabel = (type: number) =>
    t(`enums.priceType.${type}`, { defaultValue: String(type) });

  const barcodeTypeLabel = (type: number) =>
    t(`enums.barcodeType.${type}`, { defaultValue: String(type) });

  const productStatusLabel = (status: number) =>
    t(`enums.productStatus.${status}`, { defaultValue: String(status) });

  const drugTypeOptions = useMemo(
    () => DRUG_TYPE_IDS.map((value) => ({ value, label: drugTypeLabel(value) })),
    [t],
  );

  const priceTypeOptions = useMemo(
    () => PRICE_TYPE_IDS.map((value) => ({ value, label: priceTypeLabel(value) })),
    [t],
  );

  const barcodeTypeOptions = useMemo(
    () => BARCODE_TYPE_IDS.map((value) => ({ value, label: barcodeTypeLabel(value) })),
    [t],
  );

  const productStatusOptions = useMemo(
    () => PRODUCT_STATUS_IDS.map((value) => ({ value, label: productStatusLabel(value) })),
    [t],
  );

  return {
    drugTypeLabel,
    drugTypeOptions,
    priceTypeLabel,
    priceTypeOptions,
    barcodeTypeLabel,
    barcodeTypeOptions,
    productStatusLabel,
    productStatusOptions,
  };
}
