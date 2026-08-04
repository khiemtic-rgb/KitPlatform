/** Client Seen-ack for kid relation moments (not study evidence). */
const PREFIX = 'famixa.kid_moment.seen.v1.';

function key(viewerId: string): string {
  return `${PREFIX}${viewerId || 'anon'}`;
}

function readIds(viewerId: string): Set<string> {
  try {
    const raw = localStorage.getItem(key(viewerId));
    if (!raw) return new Set();
    const arr = JSON.parse(raw) as unknown;
    if (!Array.isArray(arr)) return new Set();
    return new Set(arr.map(String).filter(Boolean));
  } catch {
    return new Set();
  }
}

function writeIds(viewerId: string, ids: Set<string>): void {
  try {
    localStorage.setItem(key(viewerId), JSON.stringify([...ids].slice(-80)));
  } catch {
    /* ignore */
  }
}

export function isKidMomentSeen(viewerId: string, memoryId: string): boolean {
  return readIds(viewerId).has(memoryId);
}

export function markKidMomentSeen(viewerId: string, memoryId: string): void {
  const s = readIds(viewerId);
  s.add(memoryId);
  writeIds(viewerId, s);
}

export function markKidMomentsSeen(viewerId: string, memoryIds: string[]): void {
  const s = readIds(viewerId);
  for (const id of memoryIds) if (id) s.add(id);
  writeIds(viewerId, s);
}

export function isKidMomentAudio(entry: {
  icon?: string | null;
  photoUrl?: string | null;
}): boolean {
  if (entry.icon === '\u{1F3A4}' || entry.icon === 'mic') return true;
  const url = (entry.photoUrl || '').split('?')[0].toLowerCase();
  return /\.(webm|m4a|mp3|ogg|aac|wav)$/.test(url);
}