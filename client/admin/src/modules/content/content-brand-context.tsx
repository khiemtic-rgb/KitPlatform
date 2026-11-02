import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  BRAND_ALL,
  canWriteInBrand,
  isBrandGuid,
  nextFilmParam,
  parseBrandParam,
  readLastBrandId,
  writeLastBrandId,
  type BrandScope,
} from './content-brand-scope';

type BrandWorkspace = {
  scope: BrandScope;
  brandId?: string;
  filmId?: string;
  canWrite: boolean;
  setBrand: (next: string | 'all' | null) => void;
  setFilm: (filmId: string | null) => void;
};

const Ctx = createContext<BrandWorkspace | null>(null);

export function ContentBrandProvider({ children }: { children: ReactNode }) {
  const [params, setParams] = useSearchParams();
  const brandRaw = params.get('brand');
  const filmRaw = params.get('film');
  const scope = parseBrandParam(brandRaw);
  const filmId = isBrandGuid(filmRaw) ? filmRaw : undefined;

  useEffect(() => {
    if (brandRaw !== null) return;
    const last = readLastBrandId(window.sessionStorage);
    if (!last) return;
    const next = new URLSearchParams(params);
    next.set('brand', last);
    setParams(next, { replace: true });
  }, [brandRaw, params, setParams]);

  const api = useMemo<BrandWorkspace>(() => {
    const setBrand = (next: string | 'all' | null) => {
      const previous = scope.mode === 'one' ? scope.brandId : null;
      const nextBrand = next && next !== BRAND_ALL ? next : null;
      const keptFilm = nextFilmParam(previous, nextBrand, filmId ?? null);
      const query = new URLSearchParams(params);
      if (next === null) {
        query.delete('brand');
        writeLastBrandId(window.sessionStorage, null);
      } else if (next === BRAND_ALL) {
        query.set('brand', BRAND_ALL);
      } else if (isBrandGuid(next)) {
        query.set('brand', next);
        writeLastBrandId(window.sessionStorage, next);
      } else {
        return;
      }
      if (keptFilm) query.set('film', keptFilm);
      else query.delete('film');
      setParams(query, { replace: true });
    };
    const setFilm = (next: string | null) => {
      if (scope.mode !== 'one') return;
      const query = new URLSearchParams(params);
      if (next && isBrandGuid(next)) query.set('film', next);
      else query.delete('film');
      setParams(query, { replace: true });
    };
    return {
      scope,
      brandId: scope.mode === 'one' ? scope.brandId : undefined,
      filmId,
      canWrite: canWriteInBrand(scope),
      setBrand,
      setFilm,
    };
  }, [filmId, params, scope, setParams]);

  return <Ctx.Provider value={api}>{children}</Ctx.Provider>;
}

export function useContentBrand(): BrandWorkspace {
  const value = useContext(Ctx);
  if (!value) throw new Error('useContentBrand requires ContentBrandProvider');
  return value;
}
