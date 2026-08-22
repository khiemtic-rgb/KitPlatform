/** Namespaces local KF/TTS so two bản dựng do not share SH01 pixels. */

let scope = '';

export function famixaMediaScope() {
  return scope;
}

export function setFamixaMediaScope(buildId?: string) {
  scope = (buildId ?? '').trim();
}

export function famixaScopedKey(prefix: string, id: string) {
  const clip = id.trim();
  return scope ? `${prefix}:${scope}:${clip}` : `${prefix}:${clip}`;
}

export function famixaLegacyKey(prefix: string, id: string) {
  return `${prefix}:${id.trim()}`;
}
