export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ProductListItem {
  id: string;
  productCode: string;
  productName: string;
  genericName?: string;
  drugType: number;
  categoryName?: string;
  brandName?: string;
  primaryBarcode?: string;
  retailPrice?: number;
  primaryImageUrl?: string;
  saleUnitName?: string;
  status: number;
}

export interface ProductImage {
  id: string;
  imageUrl: string;
  sortOrder: number;
  isPrimary: boolean;
}

export interface ProductDetail {
  id: string;
  productCode: string;
  productName: string;
  genericName?: string;
  drugType: number;
  categoryId?: string;
  brandId?: string;
  description?: string;
  status: number;
  saleUnitName?: string;
  units: ProductUnit[];
  barcodes: ProductBarcode[];
  prices: ProductPrice[];
  images: ProductImage[];
  ingredients: ProductIngredient[];
}

export interface ProductIngredient {
  id: string;
  ingredientId: string;
  ingredientCode: string;
  ingredientName: string;
  strengthValue?: number;
  strengthUnit?: string;
}

export interface ProductUnit {
  id: string;
  unitName: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  isSaleUnit: boolean;
}

export interface ProductBarcode {
  id: string;
  barcode: string;
  barcodeType: number;
  isPrimary: boolean;
}

export interface ProductPrice {
  id: string;
  productUnitId: string;
  unitName: string;
  priceType: number;
  currencyCode: string;
  price: number;
  effectiveFrom: string;
  effectiveTo?: string;
}

export interface LookupItem {
  id: string;
  code: string;
  name: string;
}

export interface Category {
  id: string;
  categoryCode: string;
  categoryName: string;
  description?: string;
  parentId?: string;
  parentName?: string;
  sortOrder: number;
  status: number;
}

export interface Brand {
  id: string;
  brandCode: string;
  brandName: string;
  countryCode?: string;
  status: number;
}

export interface ActiveIngredient {
  id: string;
  ingredientCode: string;
  ingredientName: string;
  description?: string;
  status: number;
}

export interface ProductListFilter {
  search?: string;
  drugTypes?: number[];
  categoryIds?: string[];
  brandIds?: string[];
  status?: number;
  priceMin?: number;
  priceMax?: number;
  hasBarcode?: boolean;
  hasPrice?: boolean;
  page?: number;
  pageSize?: number;
}

export const SALE_UNIT_OPTIONS = [
  'Viên',
  'Hộp',
  'Chai',
  'Tuýp',
  'Gói',
  'Lọ',
  'Ống',
  'Vỉ',
  'Hộp con',
].map((u) => ({ value: u, label: u }));

export const BARCODE_TYPE_LABELS: Record<number, string> = {
  1: 'Nhà sản xuất',
  2: 'Nội bộ',
  3: 'QR',
  4: 'GS1',
};

export const DRUG_TYPE_LABELS: Record<number, string> = {
  1: 'OTC',
  2: 'Kê đơn',
  3: 'Kiểm soát',
};

export const PRICE_TYPE_LABELS: Record<number, string> = {
  1: 'Bán lẻ',
  2: 'Bán buôn',
  3: 'VIP',
  4: 'Bảo hiểm',
  5: 'Online',
};

export const STATUS_LABELS: Record<number, string> = {
  1: 'Đang bán',
  2: 'Ngừng',
};
