import { useCallback, useEffect, useState } from 'react';

function readStored<T>(key: string): T | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

/**
 * Nhớ bộ lọc list trên localStorage (theo key trang).
 * Chỉ persist khi giá trị khác default (tránh ghi rác).
 */
export function usePersistedFilters<T extends Record<string, unknown>>(
  storageKey: string,
  defaults: T,
): [T, (next: T | ((prev: T) => T)) => void, () => void] {
  const [filters, setFiltersState] = useState<T>(() => {
    const stored = readStored<Partial<T>>(storageKey);
    return stored ? { ...defaults, ...stored } : defaults;
  });

  useEffect(() => {
    try {
      localStorage.setItem(storageKey, JSON.stringify(filters));
    } catch {
      /* quota / private mode */
    }
  }, [filters, storageKey]);

  const setFilters = useCallback((next: T | ((prev: T) => T)) => {
    setFiltersState((prev) => (typeof next === 'function' ? (next as (p: T) => T)(prev) : next));
  }, []);

  const resetFilters = useCallback(() => {
    setFiltersState(defaults);
    try {
      localStorage.removeItem(storageKey);
    } catch {
      /* ignore */
    }
  }, [defaults, storageKey]);

  return [filters, setFilters, resetFilters];
}
