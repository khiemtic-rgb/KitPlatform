import { isAxiosError } from 'axios';
import { http } from '@/shared/api/http';
import type {
  ActiveIngredient,
  Brand,
  Category,
  LookupItem,
  PagedResult,
  ProductDetail,
  ProductListFilter,
  ProductListItem,
  ProductBarcode,
  ProductPrice,
} from '@/shared/api/catalog.types';

export interface ProductImageInput {
  imageUrl: string;
  isPrimary?: boolean;
  sortOrder?: number;
}

export interface SimilarProductMatch {
  id: string;
  productCode: string;
  productName: string;
  similarityScore: number;
}

export interface SimilarProductNamesResult {
  matches: SimilarProductMatch[];
  hasExactNormalizedMatch: boolean;
}

export interface ProductCommercialPayload {
  barcodes: { barcode: string; isPrimary: boolean; barcodeType?: number }[];
  prices: { priceType: number; price: number; productUnitId?: string }[];
  images: ProductImageInput[];
}

export interface ProductSavePayload {
  productCode?: string;
  productName: string;
  genericName?: string;
  drugType: number;
  categoryId?: string;
  brandId?: string;
  description?: string;
  status?: number;
  saleUnitName?: string;
}

export interface ProductUnitPayload {
  id?: string;
  unitName: string;
  conversionFactor: number;
  isBaseUnit: boolean;
  isSaleUnit: boolean;
}

export interface ProductIngredientPayload {
  ingredientId: string;
  strengthValue?: number;
  strengthUnit?: string;
}

export interface BarcodeCheckResult {
  isAvailable: boolean;
  existingProductId?: string;
  existingProductCode?: string;
  existingProductName?: string;
}

function cleanParams(params: ProductListFilter): Record<string, unknown> {
  const entries = Object.entries(params).filter(([, value]) => {
    if (value === undefined || value === null || value === '') return false;
    if (Array.isArray(value) && value.length === 0) return false;
    return true;
  });
  return Object.fromEntries(entries);
}

function normalizeProductListItem(item: ProductListItem & Record<string, unknown>): ProductListItem {
  return {
    ...item,
    id: String(item.id ?? item.Id),
    productCode: String(item.productCode ?? item.ProductCode ?? ''),
    productName: String(item.productName ?? item.ProductName ?? ''),
    genericName: (item.genericName ?? item.GenericName) as string | undefined,
    drugType: Number(item.drugType ?? item.DrugType ?? 1),
    categoryName: (item.categoryName ?? item.CategoryName) as string | undefined,
    brandName: (item.brandName ?? item.BrandName) as string | undefined,
    primaryBarcode: (item.primaryBarcode ?? item.PrimaryBarcode) as string | undefined,
    retailPrice: (item.retailPrice ?? item.RetailPrice) as number | undefined,
    primaryImageUrl: (item.primaryImageUrl ?? item.PrimaryImageUrl) as string | undefined,
    saleUnitName: (item.saleUnitName ?? item.SaleUnitName) as string | undefined,
    status: Number(item.status ?? item.Status ?? 1),
  };
}

function normalizePagedResult<T>(data: PagedResult<T> & Record<string, unknown>): PagedResult<T> {
  const rawItems = (data.items ?? data.Items ?? []) as Array<T & Record<string, unknown>>;
  const items = rawItems.map((item) => normalizeProductListItem(item as ProductListItem & Record<string, unknown>)) as T[];
  const total = Number(data.total ?? data.Total ?? items.length);
  const page = Number(data.page ?? data.Page ?? 1);
  const pageSize = Number(data.pageSize ?? data.PageSize ?? 20);
  return { items, total, page, pageSize };
}

export async function fetchProducts(params: ProductListFilter): Promise<PagedResult<ProductListItem>> {
  const { data } = await http.get<PagedResult<ProductListItem> & Record<string, unknown>>('/catalog/products', {
    params: cleanParams(params),
  });
  return normalizePagedResult<ProductListItem>(data);
}

function normalizeProductDetail(data: ProductDetail & Record<string, unknown>): ProductDetail {
  const units = (data.units ?? data.Units ?? []) as ProductDetail['units'];
  return {
    ...data,
    id: String(data.id ?? data.Id),
    productCode: String(data.productCode ?? data.ProductCode ?? ''),
    productName: String(data.productName ?? data.ProductName ?? ''),
    genericName: (data.genericName ?? data.GenericName) as string | undefined,
    drugType: Number(data.drugType ?? data.DrugType ?? 1),
    categoryId: (data.categoryId ?? data.CategoryId) as string | undefined,
    brandId: (data.brandId ?? data.BrandId) as string | undefined,
    description: (data.description ?? data.Description) as string | undefined,
    status: Number(data.status ?? data.Status ?? 1),
    saleUnitName: (data.saleUnitName ?? data.SaleUnitName) as string | undefined,
    units: units.map((u) => ({
      ...u,
      id: String((u as { id?: string; Id?: string }).id ?? (u as { Id?: string }).Id),
      isBaseUnit: Boolean((u as { isBaseUnit?: boolean; IsBaseUnit?: boolean }).isBaseUnit ?? (u as { IsBaseUnit?: boolean }).IsBaseUnit),
    })),
    barcodes: ((data.barcodes ?? data.Barcodes ?? []) as Array<ProductDetail['barcodes'][number] & Record<string, unknown>>).map(
      (b) => ({
        id: String(b.id ?? b.Id ?? ''),
        barcode: String(b.barcode ?? b.Barcode ?? ''),
        barcodeType: Number(b.barcodeType ?? b.BarcodeType ?? 1),
        isPrimary: Boolean(b.isPrimary ?? b.IsPrimary),
      }),
    ),
    prices: ((data.prices ?? data.Prices ?? []) as Array<ProductDetail['prices'][number] & Record<string, unknown>>).map(
      (p) => ({
        id: String(p.id ?? p.Id ?? ''),
        productUnitId: String(p.productUnitId ?? p.ProductUnitId ?? ''),
        unitName: String(p.unitName ?? p.UnitName ?? ''),
        priceType: Number(p.priceType ?? p.PriceType ?? 1),
        currencyCode: String(p.currencyCode ?? p.CurrencyCode ?? 'VND'),
        price: Number(p.price ?? p.Price ?? 0),
        effectiveFrom: String(p.effectiveFrom ?? p.EffectiveFrom ?? ''),
        effectiveTo: (p.effectiveTo ?? p.EffectiveTo) as string | undefined,
      }),
    ),
    images: ((data.images ?? data.Images ?? []) as Array<ProductDetail['images'][number] & Record<string, unknown>>).map(
      (img) => ({
        id: String(img.id ?? img.Id ?? ''),
        imageUrl: String(img.imageUrl ?? img.ImageUrl ?? ''),
        sortOrder: Number(img.sortOrder ?? img.SortOrder ?? 0),
        isPrimary: Boolean(img.isPrimary ?? img.IsPrimary),
      }),
    ),
    ingredients: (
      (data.ingredients ?? data.Ingredients ?? []) as Array<ProductDetail['ingredients'][number] & Record<string, unknown>>
    ).map((ing) => ({
      id: String(ing.id ?? ing.Id ?? ''),
      ingredientId: String(ing.ingredientId ?? ing.IngredientId ?? ''),
      ingredientCode: String(ing.ingredientCode ?? ing.IngredientCode ?? ''),
      ingredientName: String(ing.ingredientName ?? ing.IngredientName ?? ''),
      strengthValue: (ing.strengthValue ?? ing.StrengthValue) as number | undefined,
      strengthUnit: (ing.strengthUnit ?? ing.StrengthUnit) as string | undefined,
    })),
  };
}

function normalizeClientProductName(name: string): string {
  return name
    .trim()
    .replace(/\s+/g, ' ')
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');
}

function mapSimilarMatches(
  data: SimilarProductNamesResult & Record<string, unknown>,
): SimilarProductNamesResult {
  const rawMatches = (data.matches ?? data.Matches ?? []) as Array<SimilarProductMatch & Record<string, unknown>>;
  return {
    matches: rawMatches.map((m) => ({
      id: String(m.id ?? m.Id ?? ''),
      productCode: String(m.productCode ?? m.ProductCode ?? ''),
      productName: String(m.productName ?? m.ProductName ?? ''),
      similarityScore: Number(m.similarityScore ?? m.SimilarityScore ?? 0),
    })),
    hasExactNormalizedMatch: Boolean(data.hasExactNormalizedMatch ?? data.HasExactNormalizedMatch),
  };
}

async function findExactNameMatchesClient(name: string, excludeId?: string): Promise<SimilarProductMatch[]> {
  const trimmed = name.trim();
  const normalized = normalizeClientProductName(trimmed);
  if (!normalized) return [];

  const data = await fetchProducts({ page: 1, pageSize: 500 });
  return (data.items ?? [])
    .filter((p) => p.id !== excludeId)
    .filter(
      (p) =>
        normalizeClientProductName(p.productName) === normalized ||
        p.productName.trim().toLowerCase() === trimmed.toLowerCase(),
    )
    .map((p) => ({
      id: p.id,
      productCode: p.productCode,
      productName: p.productName,
      similarityScore: 1,
    }));
}

export async function checkSimilarProductNames(
  name: string,
  excludeId?: string,
): Promise<SimilarProductNamesResult> {
  const empty: SimilarProductNamesResult = { matches: [], hasExactNormalizedMatch: false };
  const trimmed = name.trim();
  if (!trimmed) return empty;

  const paths = ['/catalog/product-check-name', '/catalog/products/check-name'];
  const params = { name: trimmed, excludeId: excludeId || undefined };

  for (const url of paths) {
    try {
      const { data } = await http.get<SimilarProductNamesResult & Record<string, unknown>>(url, { params });
      const result = mapSimilarMatches(data);
      if (result.matches.length > 0) return result;
    } catch (error) {
      if (isAxiosError(error) && error.response?.status === 404) continue;
    }
  }

  try {
    const clientMatches = await findExactNameMatchesClient(trimmed, excludeId);
    if (clientMatches.length > 0) {
      return { matches: clientMatches, hasExactNormalizedMatch: true };
    }
  } catch {
    /* ignore */
  }

  return empty;
}

export async function fetchProduct(id: string): Promise<ProductDetail> {
  const { data } = await http.get<ProductDetail & Record<string, unknown>>(`/catalog/products/${id}`);
  return normalizeProductDetail(data);
}

export async function fetchNextProductCode(): Promise<string> {
  const apiPaths: Array<string | { url: string; params?: Record<string, unknown> }> = [
    { url: '/catalog/products', params: { nextCodeOnly: true } },
    '/catalog/product-next-code',
    '/catalog/products/next-code',
  ];

  let lastError: unknown;

  for (const entry of apiPaths) {
    try {
      const request = typeof entry === 'string' ? { url: entry } : entry;
      const { data } = await http.get<unknown>(request.url, { params: request.params });
      const code = extractProductCode(data);
      if (code) return code;
      lastError = new Error(`API ${request.url} không trả về mã sản phẩm`);
    } catch (error) {
      lastError = error;
    }
  }

  try {
    return await deriveNextProductCodeFromList();
  } catch (error) {
    throw lastError ?? error;
  }
}

async function deriveNextProductCodeFromList(): Promise<string> {
  const data = await fetchProducts({ page: 1, pageSize: 500 });
  const maxNumber = (data.items ?? [])
    .map((item) => item.productCode)
    .filter((code) => /^SP-\d+$/i.test(code))
    .map((code) => Number.parseInt(code.slice(3), 10))
    .filter((value) => Number.isFinite(value))
    .reduce((max, value) => Math.max(max, value), 0);

  return `SP-${String(maxNumber + 1).padStart(6, '0')}`;
}

function extractProductCode(data: unknown): string {
  if (typeof data === 'string') return data.trim();
  if (!data || typeof data !== 'object') return '';

  const raw = data as Record<string, unknown>;
  const direct = raw.productCode ?? raw.ProductCode ?? raw.code ?? raw.Code;
  if (typeof direct === 'string') return direct.trim();

  const nested = raw.data ?? raw.Data;
  if (nested && typeof nested === 'object') {
    const inner = nested as Record<string, unknown>;
    const value = inner.productCode ?? inner.ProductCode ?? inner.code ?? inner.Code;
    if (typeof value === 'string') return value.trim();
  }

  return '';
}

function toGeneralBody(body: ProductSavePayload) {
  return {
    productCode: body.productCode ?? null,
    productName: body.productName,
    genericName: body.genericName ?? null,
    drugType: body.drugType,
    categoryId: body.categoryId ?? null,
    brandId: body.brandId ?? null,
    description: body.description ?? null,
    status: body.status ?? 1,
    saleUnitName: body.saleUnitName ?? null,
  };
}

function toUnitsBody(units: ProductUnitPayload[]) {
  return {
    units: units.map((u) => ({
      id: u.id?.trim() ? u.id : null,
      unitName: u.unitName,
      conversionFactor: u.conversionFactor,
      isBaseUnit: u.isBaseUnit,
      isSaleUnit: u.isSaleUnit,
    })),
  };
}

function toIngredientsBody(ingredients: ProductIngredientPayload[]) {
  return {
    ingredients: ingredients.map((ing) => ({
      ingredientId: ing.ingredientId,
      strengthValue: ing.strengthValue ?? null,
      strengthUnit: ing.strengthUnit?.trim() ? ing.strengthUnit : null,
    })),
  };
}

function toCommercialBody(body: ProductCommercialPayload) {
  return {
    barcodes: body.barcodes.map((b) => ({
      barcode: b.barcode,
      isPrimary: b.isPrimary,
      barcodeType: b.barcodeType ?? 1,
    })),
    prices: body.prices.map((p) => ({
      priceType: p.priceType,
      price: p.price,
      productUnitId: p.productUnitId?.trim() ? p.productUnitId : null,
    })),
    images: body.images.map((img, index) => ({
      imageUrl: img.imageUrl,
      isPrimary: img.isPrimary ?? false,
      sortOrder: img.sortOrder ?? index,
    })),
  };
}

export async function createProduct(body: ProductSavePayload): Promise<ProductDetail> {
  const { data } = await http.post<ProductDetail & Record<string, unknown>>('/catalog/products', toGeneralBody(body));
  return normalizeProductDetail(data);
}

export async function updateProduct(id: string, body: ProductSavePayload): Promise<ProductDetail> {
  const { data } = await http.put<ProductDetail & Record<string, unknown>>(`/catalog/products/${id}`, toGeneralBody(body));
  return normalizeProductDetail(data);
}

export async function syncProductCommercial(id: string, body: ProductCommercialPayload): Promise<ProductDetail> {
  const commercial = toCommercialBody(body);
  const payload = { productId: id, ...commercial };
  const attempts: Array<{ method: 'put' | 'post'; url: string; data: Record<string, unknown> }> = [
    { method: 'put', url: '/catalog/product-commercial', data: payload },
    { method: 'post', url: '/catalog/product-commercial', data: payload },
    { method: 'put', url: `/catalog/products/${id}/commercial`, data: commercial },
    { method: 'post', url: `/catalog/products/${id}/commercial`, data: commercial },
  ];

  let lastError: unknown;

  for (const attempt of attempts) {
    try {
      const { data } =
        attempt.method === 'put'
          ? await http.put<ProductDetail & Record<string, unknown>>(attempt.url, attempt.data)
          : await http.post<ProductDetail & Record<string, unknown>>(attempt.url, attempt.data);
      const normalized = normalizeProductDetail(data);
      return fetchProduct(normalized.id);
    } catch (error) {
      lastError = error;
      if (isAxiosError(error) && error.response?.status === 404) continue;
      throw error;
    }
  }

  const legacy = await syncProductCommercialLegacy(id, body, lastError);
  return fetchProduct(legacy.id);
}

async function syncProductCommercialLegacy(
  id: string,
  body: ProductCommercialPayload,
  lastError: unknown,
): Promise<ProductDetail> {
  try {
    const detail = await fetchProduct(id);
    const primary = body.barcodes.find((b) => b.isPrimary) ?? body.barcodes[0];
    const extraBarcodes = body.barcodes
      .filter((b) => b.barcode !== primary?.barcode)
      .map((b) => ({ barcode: b.barcode, barcodeType: b.barcodeType ?? 1 }));

    const { data } = await http.put<ProductDetail & Record<string, unknown>>(`/catalog/products/${id}`, {
      productName: detail.productName,
      genericName: detail.genericName ?? null,
      drugType: detail.drugType,
      categoryId: detail.categoryId ?? null,
      brandId: detail.brandId ?? null,
      description: detail.description ?? null,
      status: detail.status,
      primaryBarcode: primary?.barcode ?? null,
      extraBarcodes,
      retailPrice: body.prices.find((p) => p.priceType === 1)?.price ?? null,
      retailProductUnitId: body.prices.find((p) => p.priceType === 1)?.productUnitId ?? null,
      extraPrices: body.prices
        .filter((p) => p.priceType !== 1)
        .map((p) => ({
          priceType: p.priceType,
          price: p.price,
          productUnitId: p.productUnitId ?? null,
        })),
      images: toCommercialBody(body).images,
    });
    return normalizeProductDetail(data);
  } catch (error) {
    throw lastError ?? error;
  }
}

export async function deleteProduct(id: string): Promise<void> {
  await http.delete(`/catalog/products/${id}`);
}

export async function bulkDeleteProducts(ids: string[]): Promise<number> {
  const { data } = await http.post<{ deletedCount: number }>('/catalog/products/bulk-delete', { ids });
  return data.deletedCount;
}

export async function addBarcode(
  productId: string,
  body: { barcode: string; barcodeType: number; isPrimary: boolean },
): Promise<ProductBarcode> {
  const { data } = await http.post<ProductBarcode>(`/catalog/products/${productId}/barcodes`, body);
  return data;
}

export async function addPrice(
  productId: string,
  body: { productUnitId: string; priceType: number; price: number; currencyCode?: string },
): Promise<ProductPrice> {
  const { data } = await http.post<ProductPrice>(`/catalog/products/${productId}/prices`, body);
  return data;
}

export async function fetchCategories(activeOnly = false): Promise<Category[]> {
  const { data } = await http.get<Category[]>('/catalog/categories', { params: { activeOnly } });
  return data;
}

export async function fetchCategoryLookups(): Promise<LookupItem[]> {
  const items = await fetchCategories(true);
  return items.map((c) => ({ id: c.id, code: c.categoryCode, name: c.categoryName }));
}

export async function createCategory(body: {
  categoryCode: string;
  categoryName: string;
  description?: string;
  parentId?: string;
  sortOrder?: number;
}): Promise<Category> {
  const { data } = await http.post<Category>('/catalog/categories', body);
  return data;
}

export async function updateCategory(
  id: string,
  body: {
    categoryName: string;
    description?: string;
    parentId?: string;
    sortOrder: number;
    status: number;
  },
): Promise<Category> {
  const { data } = await http.put<Category>(`/catalog/categories/${id}`, body);
  return data;
}

export async function deleteCategory(id: string): Promise<void> {
  await http.delete(`/catalog/categories/${id}`);
}

export async function fetchBrands(activeOnly = false): Promise<Brand[]> {
  const { data } = await http.get<Brand[]>('/catalog/brands', { params: { activeOnly } });
  return data;
}

export async function fetchBrandLookups(): Promise<LookupItem[]> {
  const items = await fetchBrands(true);
  return items.map((b) => ({ id: b.id, code: b.brandCode, name: b.brandName }));
}

export async function createBrand(body: {
  brandCode: string;
  brandName: string;
  countryCode?: string;
}): Promise<Brand> {
  const { data } = await http.post<Brand>('/catalog/brands', body);
  return data;
}

export async function updateBrand(
  id: string,
  body: { brandName: string; countryCode?: string; status: number },
): Promise<Brand> {
  const { data } = await http.put<Brand>(`/catalog/brands/${id}`, body);
  return data;
}

export async function deleteBrand(id: string): Promise<void> {
  await http.delete(`/catalog/brands/${id}`);
}

function normalizeActiveIngredient(raw: ActiveIngredient & Record<string, unknown>): ActiveIngredient {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    ingredientCode: String(
      raw.ingredientCode ?? raw.IngredientCode ?? raw.code ?? raw.Code ?? '',
    ),
    ingredientName: String(
      raw.ingredientName ?? raw.IngredientName ?? raw.name ?? raw.Name ?? '',
    ),
    description: (raw.description ?? raw.Description) as string | undefined,
    status: Number(raw.status ?? raw.Status ?? 1),
  };
}

export async function fetchActiveIngredients(activeOnly = false): Promise<ActiveIngredient[]> {
  const { data } = await http.get<Array<ActiveIngredient & Record<string, unknown>>>(
    '/catalog/ingredients',
    { params: { activeOnly } },
  );
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => normalizeActiveIngredient(row));
}

export async function createActiveIngredient(body: {
  ingredientCode: string;
  ingredientName: string;
  description?: string;
}): Promise<ActiveIngredient> {
  const { data } = await http.post<ActiveIngredient & Record<string, unknown>>('/catalog/ingredients', body);
  return normalizeActiveIngredient(data);
}

export async function updateActiveIngredient(
  id: string,
  body: { ingredientName: string; description?: string; status: number },
): Promise<ActiveIngredient> {
  const { data } = await http.put<ActiveIngredient & Record<string, unknown>>(
    `/catalog/ingredients/${id}`,
    body,
  );
  return normalizeActiveIngredient(data);
}

export async function deleteActiveIngredient(id: string): Promise<void> {
  await http.delete(`/catalog/ingredients/${id}`);
}

export async function fetchIngredientLookups(): Promise<LookupItem[]> {
  const items = await fetchActiveIngredients(true);
  return items.map((item) => ({
    id: item.id,
    code: item.ingredientCode,
    name: item.ingredientName,
  }));
}

export async function checkBarcode(
  barcode: string,
  excludeProductId?: string,
): Promise<BarcodeCheckResult> {
  const trimmed = barcode.trim();
  if (!trimmed) return { isAvailable: true };

  const paths = ['/catalog/product-check-barcode'];
  const params = { barcode: trimmed, excludeProductId: excludeProductId || undefined };

  for (const url of paths) {
    try {
      const { data } = await http.get<BarcodeCheckResult & Record<string, unknown>>(url, { params });
      return {
        isAvailable: Boolean(data.isAvailable ?? data.IsAvailable ?? true),
        existingProductId: (data.existingProductId ?? data.ExistingProductId) as string | undefined,
        existingProductCode: (data.existingProductCode ?? data.ExistingProductCode) as string | undefined,
        existingProductName: (data.existingProductName ?? data.ExistingProductName) as string | undefined,
      };
    } catch (error) {
      if (isAxiosError(error) && error.response?.status === 404) continue;
      throw error;
    }
  }

  return { isAvailable: true };
}

async function syncWithFallback(
  id: string,
  resource: 'units' | 'ingredients',
  body: Record<string, unknown>,
): Promise<ProductDetail> {
  const payload = { productId: id, ...body };
  const attempts: Array<{ method: 'put' | 'post'; url: string; data: Record<string, unknown> }> = [
    { method: 'put', url: `/catalog/product-${resource}`, data: payload },
    { method: 'post', url: `/catalog/product-${resource}`, data: payload },
    { method: 'put', url: `/catalog/products/${id}/${resource}`, data: body },
    { method: 'post', url: `/catalog/products/${id}/${resource}`, data: body },
  ];

  let lastError: unknown;
  for (const attempt of attempts) {
    try {
      const { data } =
        attempt.method === 'put'
          ? await http.put<ProductDetail & Record<string, unknown>>(attempt.url, attempt.data)
          : await http.post<ProductDetail & Record<string, unknown>>(attempt.url, attempt.data);
      return fetchProduct(String(data.id ?? data.Id ?? id));
    } catch (error) {
      lastError = error;
      if (isAxiosError(error) && error.response?.status === 404) continue;
      throw error;
    }
  }

  throw lastError ?? new Error(`Không đồng bộ được ${resource}`);
}

export async function syncProductUnits(id: string, units: ProductUnitPayload[]): Promise<ProductDetail> {
  return syncWithFallback(id, 'units', toUnitsBody(units));
}

export async function syncProductIngredients(
  id: string,
  ingredients: ProductIngredientPayload[],
): Promise<ProductDetail> {
  return syncWithFallback(id, 'ingredients', toIngredientsBody(ingredients));
}
