/** Famixa CHAR Master Reference v1.0 — bundled SoT so F5/server strip cannot lose faces. */

export type FamixaCanonSeed = {
  characterId: 'CHAR-001' | 'CHAR-002' | 'CHAR-003';
  names: string[];
  fileName: string;
  publicPath: string;
};

export const FAMIXA_CANON_SEED: FamixaCanonSeed[] = [
  {
    characterId: 'CHAR-001',
    names: ['minh', 'bé minh', 'con'],
    fileName: 'CHAR-001-minh-master.png',
    publicPath: '/content/famixa/canon/CHAR-001-minh-master.png',
  },
  {
    characterId: 'CHAR-002',
    names: ['nam', 'bố', 'bo', 'bố nam'],
    fileName: 'CHAR-002-nam-master.png',
    publicPath: '/content/famixa/canon/CHAR-002-nam-master.png',
  },
  {
    characterId: 'CHAR-003',
    names: ['linh', 'mẹ', 'me', 'mẹ linh'],
    fileName: 'CHAR-003-linh-master.png',
    publicPath: '/content/famixa/canon/CHAR-003-linh-master.png',
  },
];

function normCharId(raw: string) {
  const m = raw.toUpperCase().match(/CHAR\s*-?\s*(\d+)/);
  if (m) return `CHAR-${String(m[1]).padStart(3, '0')}`;
  return raw.replace(/\s+/g, ' ').trim();
}

export function famixaCanonSeedFor(c?: { id?: string; name?: string; characterId?: string }) {
  if (!c) return undefined;
  const id = normCharId(c.characterId || c.id || '');
  const byId = FAMIXA_CANON_SEED.find((s) => s.characterId === id);
  if (byId) return byId;
  const n = (c.name || '').trim().toLowerCase();
  if (!n) return undefined;
  return FAMIXA_CANON_SEED.find((s) => s.names.some((x) => n === x || n.startsWith(`${x} `)));
}

export async function fetchFamixaCanonSeedDataUrl(publicPath: string) {
  const res = await fetch(publicPath);
  if (!res.ok) throw new Error(`Không tải được Master Reference (${res.status}).`);
  const blob = await res.blob();
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.onerror = () => reject(new Error('Không đọc được Master Reference.'));
    reader.readAsDataURL(blob);
  });
}
