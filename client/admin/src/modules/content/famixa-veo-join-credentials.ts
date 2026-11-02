/** Veo Join file is not a Gemini API key. Shape only — 0 provider calls. */

export const VEO_JOIN_MAX_BYTES = 64 * 1024;

export function isGeminiApiKeyLookalike(raw: string | null | undefined): boolean {
  const t = (raw ?? '').trim();
  if (t.length < 20) return false;
  if (t.startsWith('AIza') || t.startsWith('AQ.')) return true;
  if (t.includes('{') || t.includes('\n')) return false;
  if (t.length < 30 || t.length > 90) return false;
  return /^[A-Za-z0-9._-]+$/.test(t);
}

export function sanitizeVeoJoinFileName(fileName: string | null | undefined): string | null {
  if (!fileName?.trim()) return null;
  const name = fileName.replace(/\\/g, '/').split('/').pop()?.trim();
  return name || null;
}

export function acceptVeoJoinFile(raw: string | null | undefined, fileName?: string | null): {
  ok: boolean;
  code?: string;
  message?: string;
  text?: string;
  fileName?: string | null;
} {
  const text = (raw ?? '').trim();
  if (!text) return { ok: false, code: 'VEO_JOIN_FILE_MISSING', message: 'Chưa chọn file Join Veo.' };
  if (new TextEncoder().encode(text).length > VEO_JOIN_MAX_BYTES)
    return { ok: false, code: 'VEO_JOIN_FILE_TOO_LARGE', message: 'File Join vượt 64KB.' };
  if (isGeminiApiKeyLookalike(text))
    return { ok: false, code: 'GEMINI_API_KEY_REJECTED', message: 'File Join Veo không phải Gemini API key.' };
  return { ok: true, text, fileName: sanitizeVeoJoinFileName(fileName) };
}
