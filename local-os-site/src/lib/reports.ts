import { feedToken } from './live-feed';

export type ReaderReport = {
  id: string;
  listingId: string;
  reason: string;
  note: string | null;
  createdAt: string;
};

const KEY = 'reports:inbox';
const MAX = 300;

type Kv = { get: (k: string) => Promise<string | null>; put: (k: string, v: string) => Promise<void> };
type ReportEnv = { HITS?: Kv };

async function env(): Promise<ReportEnv | null> {
  try {
    const mod = await import('cloudflare:workers');
    return ((mod as { env?: ReportEnv }).env) ?? null;
  } catch {
    return null;
  }
}

async function readAll(): Promise<ReaderReport[]> {
  const ns = (await env())?.HITS;
  if (!ns) return [];
  const raw = await ns.get(KEY);
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw) as { reports?: ReaderReport[] };
    return Array.isArray(parsed.reports) ? parsed.reports : [];
  } catch {
    return [];
  }
}

export async function hasKvReports(): Promise<boolean> {
  return !!(await env())?.HITS;
}

export async function appendReport(input: {
  listingId: string;
  reason: string;
  note?: string | null;
}): Promise<'ok' | 'dup'> {
  const listingId = input.listingId.trim();
  const reason = input.reason.trim().toLowerCase();
  const note = input.note?.trim() ? input.note.trim().slice(0, 280) : null;
  const now = Date.now();
  const all = await readAll();
  const sameDay = all.filter(
    (r) => r.listingId === listingId && now - Date.parse(r.createdAt) < 24 * 3600 * 1000,
  );
  if (sameDay.length >= 8) return 'dup';
  const recent = all.find(
    (r) =>
      r.listingId === listingId &&
      r.reason === reason &&
      now - Date.parse(r.createdAt) < 20 * 60 * 1000,
  );
  if (recent) return 'dup';

  const next: ReaderReport = {
    id: crypto.randomUUID(),
    listingId,
    reason,
    note,
    createdAt: new Date().toISOString(),
  };
  const reports = [next, ...all].slice(0, MAX);
  const ns = (await env())?.HITS;
  if (!ns) throw new Error('KV chưa sẵn sàng.');
  await ns.put(KEY, JSON.stringify({ reports }));
  return 'ok';
}

export async function listReports(): Promise<ReaderReport[]> {
  return readAll();
}

export { feedToken };
