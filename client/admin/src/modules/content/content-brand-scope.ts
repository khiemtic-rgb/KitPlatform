export const BRAND_ALL = 'all';
const LAST_BRAND_KEY = 'kit-mkt.brand.last';
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type BrandScope =
  | { mode: 'missing' }
  | { mode: 'invalid' }
  | { mode: 'all' }
  | { mode: 'one'; brandId: string };

export function isBrandGuid(value: string | null | undefined): value is string {
  return !!value && GUID.test(value);
}

export function parseBrandParam(raw: string | null | undefined): BrandScope {
  if (raw == null || raw.trim() === '') return { mode: 'missing' };
  if (raw === BRAND_ALL) return { mode: 'all' };
  if (isBrandGuid(raw)) return { mode: 'one', brandId: raw };
  return { mode: 'invalid' };
}

export function readLastBrandId(storage: Pick<Storage, 'getItem'> | null): string | null {
  if (!storage) return null;
  const value = storage.getItem(LAST_BRAND_KEY);
  return isBrandGuid(value) ? value : null;
}

export function writeLastBrandId(storage: Pick<Storage, 'setItem' | 'removeItem'> | null, brandId: string | null) {
  if (!storage) return;
  if (brandId && isBrandGuid(brandId)) storage.setItem(LAST_BRAND_KEY, brandId);
  else storage.removeItem(LAST_BRAND_KEY);
}

export function canWriteInBrand(scope: BrandScope): boolean {
  return scope.mode === 'one';
}

/** Changing the brand drops a film type that belonged to the previous brand. */
export function nextFilmParam(previousBrand: string | null, nextBrand: string | null, film: string | null): string | null {
  if (!film) return null;
  if (previousBrand && nextBrand && previousBrand === nextBrand) return film;
  return null;
}
