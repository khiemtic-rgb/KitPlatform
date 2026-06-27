export type ReportCategory = 'sales' | 'procurement' | 'inventory';

export type ReportGroupByOption = 'day' | 'week' | 'month' | 'supplier';

export interface ReportDefinition {
  code: string;
  name: string;
  description: string;
  category: ReportCategory;
  path: string;
  /** API path segment under /reports */
  apiPath: string;
  supportsDateRange?: boolean;
  supportsGroupBy?: ReportGroupByOption[];
  supportsWarehouse?: boolean;
  supportsSupplier?: boolean;
  supportsSearch?: boolean;
  supportsExpiryDays?: boolean;
  favorite?: boolean;
}

export const REPORT_CATEGORY_LABELS: Record<ReportCategory, string> = {
  sales: 'Bán hàng',
  procurement: 'Mua hàng',
  inventory: 'Kho hàng',
};

export const REPORT_DEFINITIONS: ReportDefinition[] = [
  {
    code: 'SALES-01',
    name: 'Doanh thu theo kỳ',
    description: 'Thu bán, hoàn trả và thu ròng theo ngày/tuần/tháng (giờ VN).',
    category: 'sales',
    path: '/reports/sales/revenue-by-period',
    apiPath: 'sales/revenue-by-period',
    supportsDateRange: true,
    supportsGroupBy: ['day', 'week', 'month'],
    supportsWarehouse: true,
    favorite: true,
  },
  {
    code: 'SALES-02',
    name: 'Doanh thu theo hình thức TT',
    description: 'Thu ròng theo tiền mặt, thẻ, chuyển khoản, ví.',
    category: 'sales',
    path: '/reports/sales/revenue-by-payment-method',
    apiPath: 'sales/revenue-by-payment-method',
    supportsDateRange: true,
    supportsWarehouse: true,
    favorite: true,
  },
  {
    code: 'SALES-03',
    name: 'Báo cáo ca làm việc',
    description: 'Danh sách ca, quỹ tiền mặt và thu ròng trong ca.',
    category: 'sales',
    path: '/reports/sales/shifts',
    apiPath: 'sales/shifts',
    supportsDateRange: true,
    supportsWarehouse: true,
    favorite: true,
  },
  {
    code: 'PROC-01',
    name: 'Giá trị nhập hàng (GRN)',
    description: 'Tổng hợp phiếu nhập hoàn tất — tiền trước thuế GTGT.',
    category: 'procurement',
    path: '/reports/procurement/grn-value',
    apiPath: 'procurement/grn-value',
    supportsDateRange: true,
    supportsGroupBy: ['supplier', 'month', 'day'],
    supportsWarehouse: true,
    supportsSupplier: true,
    favorite: true,
  },
  {
    code: 'PROC-03',
    name: 'Công nợ NCC (snapshot)',
    description: 'Số còn phải trả và phân tích tuổi nợ tại thời điểm xem.',
    category: 'procurement',
    path: '/reports/procurement/payables-snapshot',
    apiPath: 'procurement/payables-snapshot',
    favorite: true,
  },
  {
    code: 'INV-01',
    name: 'Tồn kho & giá trị',
    description: 'Số lượng và giá trị tồn (qty × giá vốn lô) theo sản phẩm/kho.',
    category: 'inventory',
    path: '/reports/inventory/stock-snapshot',
    apiPath: 'inventory/stock-snapshot',
    supportsWarehouse: true,
    supportsSearch: true,
    favorite: true,
  },
  {
    code: 'INV-02',
    name: 'Sắp hết hạn sử dụng',
    description: 'Lô tồn có HSD trong số ngày cảnh báo.',
    category: 'inventory',
    path: '/reports/inventory/near-expiry',
    apiPath: 'inventory/near-expiry',
    supportsWarehouse: true,
    supportsExpiryDays: true,
    favorite: true,
  },
];

export function findReportByPath(pathname: string): ReportDefinition | undefined {
  return REPORT_DEFINITIONS.find((r) => pathname.startsWith(r.path));
}

export function reportsForCategory(category: ReportCategory): ReportDefinition[] {
  return REPORT_DEFINITIONS.filter((r) => r.category === category);
}
