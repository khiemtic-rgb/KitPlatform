const KEY = 'famixa.parent.nudges.v1';
const VERIFIED_KEY = 'famixa.parent.verified.v1';

/** familyId -> date -> count (preferred). Legacy flat date -> count still readable. */
type NestedStore = Record<string, Record<string, number>>;
type LegacyStore = Record<string, number>;

function isNested(store: NestedStore | LegacyStore): store is NestedStore {
  const first = Object.values(store)[0];
  return first != null && typeof first === 'object';
}

function readRaw(): NestedStore | LegacyStore {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as NestedStore | LegacyStore;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function writeNested(store: NestedStore) {
  localStorage.setItem(KEY, JSON.stringify(store));
}

function ensureNested(): NestedStore {
  const raw = readRaw();
  if (isNested(raw)) return raw;
  // Migrate legacy flat map under a synthetic bucket; callers with familyId get empty until hydrate.
  const nested: NestedStore = { __legacy__: raw as LegacyStore };
  writeNested(nested);
  return nested;
}

export function getNudgeCount(familyId: string, flowDate: string): number {
  const store = ensureNested();
  const byFamily = store[familyId]?.[flowDate];
  if (typeof byFamily === 'number') return Math.max(0, byFamily);
  const legacy = store.__legacy__?.[flowDate];
  return Math.max(0, Number(legacy ?? 0));
}

export function setNudgeCountLocal(familyId: string, flowDate: string, count: number) {
  const store = ensureNested();
  const row = store[familyId] ?? {};
  row[flowDate] = Math.max(0, count);
  store[familyId] = row;
  writeNested(store);
}

export function mergeNudgeCounts(familyId: string, counts: Record<string, number>) {
  const store = ensureNested();
  const row = { ...(store[familyId] ?? {}) };
  for (const [date, count] of Object.entries(counts)) {
    row[date] = Math.max(Number(row[date] ?? 0), Math.max(0, count));
  }
  store[familyId] = row;
  writeNested(store);
}

export function listLocalNudgeCounts(familyId: string): Record<string, number> {
  return { ...(ensureNested()[familyId] ?? {}) };
}

export function recordNudge(familyId: string, flowDate: string, count = 1): number {
  const next = getNudgeCount(familyId, flowDate) + Math.max(1, count);
  setNudgeCountLocal(familyId, flowDate, next);
  return next;
}

export function previousCalendarDate(flowDate: string): string {
  const d = new Date(`${flowDate}T12:00:00`);
  if (Number.isNaN(d.getTime())) return flowDate;
  d.setDate(d.getDate() - 1);
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}

type VerifiedStore = Record<string, string[]>;

function readVerified(): VerifiedStore {
  try {
    const raw = localStorage.getItem(VERIFIED_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as VerifiedStore;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

export function isParentVerified(flowDate: string, commitmentId: string): boolean {
  const list = readVerified()[flowDate] ?? [];
  return list.includes(commitmentId);
}

export function markParentVerified(flowDate: string, commitmentId: string) {
  const store = readVerified();
  const list = new Set(store[flowDate] ?? []);
  list.add(commitmentId);
  store[flowDate] = [...list];
  localStorage.setItem(VERIFIED_KEY, JSON.stringify(store));
}
