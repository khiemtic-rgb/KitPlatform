/** Short blob: URLs so React never holds multi-MB data:image strings. */

const urls = new Map<string, { src: string; sig: string }>();

export function displayUrlForData(key: string, dataUrl?: string) {
  const raw = (dataUrl ?? '').trim();
  if (!key || !raw.startsWith('data:image')) return undefined;
  const sig = `${raw.length}:${raw.slice(13, 40)}`;
  const hit = urls.get(key);
  if (hit?.sig === sig) return hit.src;
  if (hit) URL.revokeObjectURL(hit.src);
  const comma = raw.indexOf(',');
  if (comma < 0) return undefined;
  const mime = /data:([^;]+)/.exec(raw.slice(0, comma))?.[1] || 'image/png';
  try {
    const bin = atob(raw.slice(comma + 1));
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i += 1) bytes[i] = bin.charCodeAt(i);
    const src = URL.createObjectURL(new Blob([bytes], { type: mime }));
    urls.set(key, { src, sig });
    return src;
  } catch {
    return undefined;
  }
}

export function stripJsonDataUrls(raw: string) {
  if (!raw.includes('data:image')) return raw;
  return raw.replace(/"data:image\/[a-zA-Z0-9.+-]+;base64,[A-Za-z0-9+/=\s]+"/g, '""');
}
