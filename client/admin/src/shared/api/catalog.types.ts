import type {
  ActiveIngredientDto,
  BarcodeCheckResultDto,
  BrandDto,
  CategoryDto,
  CreateProductRequest,
  PagedProductListResult,
  ProductBarcodeDto,
  ProductDetailDto,
  ProductImageDto,
  ProductImageItem,
  ProductIngredientDto,
  ProductIngredientSyncItem,
  ProductListItemDto,
  ProductPriceDto,
  ProductUnitDto,
  Req,
  SimilarProductNameDto,
  SimilarProductNamesResultDto,
} from '@/shared/api/generated';

/** Phân trang chung (client). */
export type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type ProductListItem = Req<
  ProductListItemDto,
  'id' | 'productCode' | 'productName' | 'drugType' | 'status'
>;

export type PagedProductList = Omit<Req<PagedProductListResult, 'total' | 'page' | 'pageSize'>, 'items'> & {
  items: ProductListItem[];
};

export type ProductImage = Req<ProductImageDto, 'id' | 'imageUrl' | 'sortOrder' | 'isPrimary'>;

export type ProductIngredient = Req<
  ProductIngredientDto,
  'id' | 'ingredientId' | 'ingredientCode' | 'ingredientName'
>;

export type ProductUnit = Req<ProductUnitDto, 'id' | 'unitName' | 'conversionFactor' | 'isBaseUnit' | 'isSaleUnit'>;

export type ProductBarcode = Req<ProductBarcodeDto, 'id' | 'barcode' | 'barcodeType' | 'isPrimary'>;

export type ProductPrice = Req<
  ProductPriceDto,
  'id' | 'productUnitId' | 'unitName' | 'priceType' | 'currencyCode' | 'price' | 'effectiveFrom'
>;

export type ProductDetail = Omit<
  Req<ProductDetailDto, 'id' | 'productCode' | 'productName' | 'drugType' | 'status'>,
  'units' | 'barcodes' | 'prices' | 'images' | 'ingredients'
> & {
  units: ProductUnit[];
  barcodes: ProductBarcode[];
  prices: ProductPrice[];
  images: ProductImage[];
  ingredients: ProductIngredient[];
};

/** Lookup gọn cho Select (map từ Category / Brand). */
export type LookupItem = {
  id: string;
  code: string;
  name: string;
};

export type Category = Req<
  CategoryDto,
  'id' | 'categoryCode' | 'categoryName' | 'sortOrder' | 'status'
>;

export type Brand = Req<BrandDto, 'id' | 'brandCode' | 'brandName' | 'status'>;

export type ActiveIngredient = Req<
  ActiveIngredientDto,
  'id' | 'ingredientCode' | 'ingredientName' | 'status'
>;

/** Query params danh sách sản phẩm (khớp GET /catalog/products). */
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

// --- Catalog API payloads (OpenAPI) ---

export type SimilarProductMatch = Req<
  SimilarProductNameDto,
  'id' | 'productCode' | 'productName' | 'similarityScore'
>;

export type SimilarProductNamesResult = Omit<
  Req<SimilarProductNamesResultDto, 'hasExactNormalizedMatch'>,
  'matches'
> & {
  matches: SimilarProductMatch[];
};

export type ProductImageInput = Pick<ProductImageItem, 'imageUrl' | 'isPrimary' | 'sortOrder'> & {
  imageUrl: string;
};

export type BarcodeCheckResult = Req<BarcodeCheckResultDto, 'isAvailable'>;

export type ProductSavePayload = Pick<
  CreateProductRequest,
  'productCode' | 'genericName' | 'categoryId' | 'brandId' | 'description' | 'status' | 'saleUnitName'
> & {
  productName: string;
  drugType: number;
};

export type ProductUnitPayload = {
  id?: string;
  unitName: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  isSaleUnit: boolean;
};

export type ProductIngredientPayload = Req<ProductIngredientSyncItem, 'ingredientId'>;

export type ProductCommercialPayload = {
  barcodes: { barcode: string; isPrimary: boolean; barcodeType?: number }[];
  prices: { priceType: number; price: number; productUnitId?: string }[];
  images: ProductImageInput[];
};
