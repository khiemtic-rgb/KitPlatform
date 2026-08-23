/** Famixa Character Canon — SoT for every pack. Do not invent visual extras. */

export type CanonVisual = 'frame' | 'mention' | 'voice';

export type FamixaCanonRow = {
  id: string;
  name: string;
  role: string;
  visual: CanonVisual;
  aliases: string[];
};

/** Locked roster. New scripts remap to these ids. */
export const FAMIXA_CHAR_CANON: FamixaCanonRow[] = [
  { id: 'CHAR-001', name: 'Minh', role: 'Con', visual: 'frame', aliases: ['minh', 'con'] },
  { id: 'CHAR-002', name: 'Nam', role: 'Bố', visual: 'frame', aliases: ['nam', 'bố', 'bo', 'ba'] },
  { id: 'CHAR-003', name: 'Linh', role: 'Mẹ', visual: 'frame', aliases: ['linh', 'mẹ', 'me'] },
  { id: 'CHAR-004', name: 'An', role: 'Bạn', visual: 'mention', aliases: ['an', 'bạn an'] },
  { id: 'CHAR-VO', name: 'Lời bình', role: 'Lời bình', visual: 'voice', aliases: ['lời bình', 'voice-over', 'voice over', 'vo'] },
];

export const FAMIXA_CANON_VERSION = 1;

const META_SPEAKER = /^(TONE|THỜI LƯỢNG|FORMAT|STYLE|NOTE|LOC|CONTINUITY|EMOTION|CẢM XÚC|TARGET|DURATION)$/i;

export function normCanonId(raw: string) {
  const t = (raw ?? '').trim();
  if (/^CHAR-VO$/i.test(t) || /loi binh|narrator/i.test(t)) return 'CHAR-VO';
  const m = t.toUpperCase().match(/CHAR\s*-?\s*(\d+)/);
  if (m) return `CHAR-${String(m[1]).padStart(3, '0')}`;
  return t;
}

export function canonRowOf(idOrName?: string) {
  const t = (idOrName ?? '').trim();
  if (!t) return undefined;
  const id = normCanonId(t);
  const byId = FAMIXA_CHAR_CANON.find((r) => r.id === id);
  if (byId) return byId;
  const key = t.toLowerCase();
  return FAMIXA_CHAR_CANON.find((r) => r.name.toLowerCase() === key || r.aliases.includes(key));
}

export function isMetaCanonSpeaker(name?: string) {
  return META_SPEAKER.test((name ?? '').trim());
}

export function isFrameCanonId(id?: string, name?: string) {
  const row = canonRowOf(id) || canonRowOf(name);
  return row?.visual === 'frame';
}

export function isOffFrameCanon(id?: string, name?: string) {
  if (isMetaCanonSpeaker(name) || isMetaCanonSpeaker(id)) return true;
  const row = canonRowOf(id) || canonRowOf(name);
  if (!row) {
    const hay = `${id || ''} ${name || ''}`;
    if (/CHAR-TONE|^TONE$/i.test(hay)) return true;
    const n = Number((id || '').replace(/\D/g, ''));
    if (/^CHAR-\d+$/i.test(id || '') && n > 4) return true;
    return false;
  }
  return row.visual !== 'frame';
}

export function displayCanonName(id: string, name?: string) {
  const n = (name || '').trim();
  if (n && !/^CHAR-\d+/i.test(n) && !/^CHAR-VO$/i.test(n)) return n;
  return canonRowOf(id)?.name || n || id;
}

export function resolveCanonSpeaker(raw: string): FamixaCanonRow | undefined {
  const token = raw.replace(/\s*\(.*\)\s*$/, '').trim();
  if (!token || isMetaCanonSpeaker(token)) return undefined;
  return canonRowOf(token);
}

export type CanonChar = {
  id: string;
  name: string;
  role?: string;
  offFrame?: boolean;
};

export function seedFamixaCanon(existing: CanonChar[] = []): CanonChar[] {
  const map = new Map<string, CanonChar>();
  for (const row of FAMIXA_CHAR_CANON) {
    if (row.visual === 'mention') continue;
    map.set(row.id, { id: row.id, name: row.name, role: row.role, offFrame: row.visual !== 'frame' });
  }
  for (const c of existing) {
    const id = normCanonId(c.id);
    const row = resolveCanonSpeaker(c.name) || canonRowOf(id);
    if (isMetaCanonSpeaker(c.name) || isMetaCanonSpeaker(id)) continue;
    if (row) {
      const prev = map.get(row.id);
      map.set(row.id, {
        id: row.id,
        name: prev?.name && !/^CHAR-/i.test(prev.name) ? prev.name : row.name,
        role: prev?.role || row.role,
        offFrame: row.visual !== 'frame',
      });
      continue;
    }
    if (/^CHAR-\d+$/i.test(id) && Number(id.replace(/\D/g, '')) > 4) {
      map.set(id, { id, name: c.name || id, role: c.role, offFrame: true });
      continue;
    }
  }
  const named = existing.some((c) => /CHAR-004|^\s*an\s*$/i.test(`${c.id} ${c.name}`));
  if (named) {
    const an = FAMIXA_CHAR_CANON.find((r) => r.id === 'CHAR-004')!;
    map.set(an.id, { id: an.id, name: an.name, role: an.role, offFrame: true });
  }
  return [...map.values()];
}

export function frameCanonIds(ids: string[]) {
  return ids.filter((id) => !isOffFrameCanon(id));
}

const CANON_STORE_KEY = 'kit-famixa-char-canon';

/** Stamp the locked roster on this machine so the next pack inherits the same law. */
export function persistFamixaCanonLaw() {
  if (typeof localStorage === 'undefined') return;
  try {
    localStorage.setItem(
      CANON_STORE_KEY,
      JSON.stringify({
        version: FAMIXA_CANON_VERSION,
        updatedAt: new Date().toISOString(),
        roster: FAMIXA_CHAR_CANON.map((r) => ({ id: r.id, name: r.name, role: r.role, visual: r.visual })),
      }),
    );
  } catch {
    /* quota */
  }
}

export function persistedCanonVersion() {
  if (typeof localStorage === 'undefined') return 0;
  try {
    const raw = localStorage.getItem(CANON_STORE_KEY);
    if (!raw) return 0;
    const v = JSON.parse(raw) as { version?: number };
    return typeof v.version === 'number' ? v.version : 0;
  } catch {
    return 0;
  }
}
