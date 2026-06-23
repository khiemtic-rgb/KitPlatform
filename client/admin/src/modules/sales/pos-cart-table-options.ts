import { SALES_DISCOUNT_TYPES } from '@/shared/api/sales.types';

/** Dropdown loại CK gọn cho bảng giỏ POS (cột hẹp). */
export const POS_CART_DISCOUNT_TYPE_OPTIONS = [
  { value: SALES_DISCOUNT_TYPES.Percent, label: '%' },
  { value: SALES_DISCOUNT_TYPES.Fixed, label: 'Tiền' },
] as const;
