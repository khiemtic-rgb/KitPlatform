/** Deep-merge plain objects/arrays for storefront CMS overlays. Arrays from overlay replace base. */
export function deepMerge<T>(base: T, overlay: unknown): T {
  if (overlay === null || overlay === undefined) return base;
  if (Array.isArray(overlay)) return overlay as T;
  if (typeof overlay !== 'object') return overlay as T;
  if (typeof base !== 'object' || base === null || Array.isArray(base)) return overlay as T;

  const out: Record<string, unknown> = { ...(base as Record<string, unknown>) };
  for (const [key, value] of Object.entries(overlay as Record<string, unknown>)) {
    if (value === undefined) continue;
    const prev = out[key];
    if (
      value &&
      typeof value === 'object' &&
      !Array.isArray(value) &&
      prev &&
      typeof prev === 'object' &&
      !Array.isArray(prev)
    ) {
      out[key] = deepMerge(prev, value);
    } else {
      out[key] = value;
    }
  }
  return out as T;
}
