import { SALES_DISCOUNT_TYPES } from '@/shared/api/sales.types';
import { salesT } from '@/shared/i18n';

/** Dropdown loại CK gọn cho bảng giỏ POS (cột hẹp). */
export function getPosCartDiscountTypeOptions() {
  const t = salesT();
  return [
    { value: SALES_DISCOUNT_TYPES.Percent, label: '%' },
    { value: SALES_DISCOUNT_TYPES.Fixed, label: t('enums.discountType.fixed') },
  ] as const;
}
