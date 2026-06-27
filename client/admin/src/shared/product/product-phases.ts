/**
 * PharmaCore — lộ trình sản phẩm
 *
 * Giai đoạn 1: Vận hành nhà thuốc / chuỗi nhỏ (bán, kho, mua, công nợ, app KH).
 * Giai đoạn 2: Báo cáo thuế chuyên sâu / export kế toán / tích hợp HĐĐT (không gồm cấu hình loại thuế cơ bản).
 *
 * Ẩn menu ≠ xóa code/route — giữ sẵn để bật lại khi triển khai Giai đoạn 2.
 */

import type { ReactNode } from 'react';

export const CURRENT_PRODUCT_PHASE = 1 as const;

/** Khóa tính năng — dùng thống nhất menu, route guard, export Phase 2 */
export type ProductFeatureKey =
  | 'procurement.vatAdmin'
  | 'procurement.payables'
  | 'procurement.payments'
  | 'sales.customerReservations'
  | 'sales.chat'
  | 'sales.vouchers'
  | 'catalog.brands'
  | 'catalog.ingredients'
  | 'catalog.nationalDrug'
  | 'reports.taxInput'
  | 'reports.accountingExport'
  | 'reports.module';

/** Bật/tắt theo giai đoạn hiện tại */
const PHASE_1_FEATURES: Record<ProductFeatureKey, boolean> = {
  // Mua hàng — cấu hình loại thuế + PO/GRN chọn thuế; báo cáo thuế chuyên sâu thuộc Giai đoạn 2
  'procurement.vatAdmin': true,
  'procurement.payables': true,
  'procurement.payments': true,
  // Bán hàng — POS, loyalty, voucher, đặt trước & chat app KH
  'sales.customerReservations': true,
  'sales.chat': true,
  'sales.vouchers': true,
  // Danh mục — tra cứu CSDL Dược QG (mock → sandbox/live khi có tài khoản liên thông QĐ 522)
  'catalog.brands': true,
  'catalog.ingredients': true,
  'catalog.nationalDrug': true,
  // Giai đoạn 2 — chưa triển khai UI
  'reports.module': true,
  'reports.taxInput': false,
  'reports.accountingExport': false,
};

/** Giai đoạn 2: bật module thuế/kế toán chuyên sâu (merge khi CURRENT_PRODUCT_PHASE >= 2) */
const PHASE_2_FEATURE_OVERRIDES: Partial<Record<ProductFeatureKey, boolean>> = {
  'reports.taxInput': true,
  'reports.accountingExport': true,
};

export function isProductFeatureEnabled(key: ProductFeatureKey): boolean {
  if (CURRENT_PRODUCT_PHASE >= 2 && key in PHASE_2_FEATURE_OVERRIDES) {
    return PHASE_2_FEATURE_OVERRIDES[key] ?? PHASE_1_FEATURES[key];
  }
  return PHASE_1_FEATURES[key];
}

export interface ProductNavTab {
  key: string;
  label: string;
  path: string;
  icon: ReactNode;
  /** Nếu set — tab chỉ hiện khi feature bật */
  feature?: ProductFeatureKey;
}

export function filterProductNavTabs<T extends ProductNavTab>(tabs: T[]): T[] {
  return tabs.filter((tab) => !tab.feature || isProductFeatureEnabled(tab.feature));
}

/** Export Phase 2 — định nghĩa sẵn cột để backend/FE không đổi schema PO/GRN */
export const PHASE_2_ACCOUNTING_EXPORT_COLUMNS = [
  'documentType',
  'documentNumber',
  'documentDate',
  'supplierCode',
  'supplierTaxCode',
  'vatTreatmentCode',
  'vatRatePercent',
  'subtotal',
  'taxAmount',
  'totalAmount',
  'paymentStatus',
] as const;

export type Phase2AccountingExportColumn = (typeof PHASE_2_ACCOUNTING_EXPORT_COLUMNS)[number];
