export type HitDay = { total: number; paths: Record<string, number> };
export type HitStore = {
  since: string;
  total: number;
  days: Record<string, HitDay>;
};

const KEY = 'v1';
const KEEP_DAYS = 30;

const LABELS: Record<string, string> = {
  '/': 'Trang chủ',
  '/viec': 'Việc làm',
  '/tro': 'Phòng trọ',
  '/su-kien': 'Sự kiện',
  '/kham-pha': 'Khám phá',
  '/dang-tin': 'Đăng tin',
  '/thong-tin/gioi-thieu': 'Giới thiệu',
};

export function pathLabel(path: string): string {
  if (LABELS[path]) return LABELS[path];
  if (path.startsWith('/viec/')) return 'Chi tiết việc';
  if (path.startsWith('/tro/')) return 'Chi tiết phòng';
  if (path.startsWith('/su-kien/')) return 'Chi tiết sự kiện';
  if (path.startsWith('/kham-pha/')) return 'Khám phá';
  if (path.startsWith('/thong-tin/')) return 'Thông tin';
  return path;
}

export function vnDay(now = Date.now()): string {
  return new Date(now + 7 * 3600 * 1000).toISOString().slice(0, 10);
}

export function normalizePath(raw: string | null | undefined): string | null {
  if (!raw) return null;
  let path = raw.split('?')[0].split('#')[0].trim();
  if (!path.startsWith('/')) path = `/${path}`;
  if (path.length > 96) path = path.slice(0, 96);
  if (path !== '/' && path.endsWith('/')) path = path.slice(0, -1);
  if (
    path.startsWith('/api') ||
    path.startsWith('/_') ||
    path === '/thong-ke' ||
    path.startsWith('/favicon') ||
    path.startsWith('/robots')
  ) {
    return null;
  }
  return path;
}

export function isBot(ua: string | null | undefined): boolean {
  if (!ua) return true;
  return /bot|crawl|spider|slurp|preview|facebookexternalhit|pingdom|lighthouse|headless/i.test(ua);
}

type HitsEnv = { HITS?: { get: (k: string) => Promise<string | null>; put: (k: string, v: string) => Promise<void> } };

async function kv() {
  try {
    const mod = await import('cloudflare:workers');
    return ((mod as { env?: HitsEnv }).env?.HITS) ?? null;
  } catch {
    return null;
  }
}

function emptyStore(since = vnDay()): HitStore {
  return { since, total: 0, days: {} };
}

function prune(store: HitStore): HitStore {
  const days = Object.keys(store.days).sort();
  if (days.length <= KEEP_DAYS) return store;
  for (const day of days.slice(0, days.length - KEEP_DAYS)) delete store.days[day];
  return store;
}

export async function readHits(): Promise<HitStore> {
  const ns = await kv();
  if (!ns) return emptyStore();
  const raw = await ns.get(KEY);
  if (!raw) return emptyStore();
  try {
    const parsed = JSON.parse(raw) as HitStore;
    return {
      since: parsed.since || vnDay(),
      total: Number(parsed.total) || 0,
      days: parsed.days && typeof parsed.days === 'object' ? parsed.days : {},
    };
  } catch {
    return emptyStore();
  }
}

export async function recordHit(path: string): Promise<void> {
  const ns = await kv();
  if (!ns) return;
  const store = prune(await readHits());
  const day = vnDay();
  store.days[day] ??= { total: 0, paths: {} };
  store.days[day].total += 1;
  store.days[day].paths[path] = (store.days[day].paths[path] ?? 0) + 1;
  store.total += 1;
  if (!store.since) store.since = day;
  await ns.put(KEY, JSON.stringify(store));
}

export function summarize(store: HitStore) {
  const today = vnDay();
  const todayRow = store.days[today] ?? { total: 0, paths: {} };
  const last7 = Object.entries(store.days)
    .sort(([a], [b]) => a.localeCompare(b))
    .slice(-7);
  const week = last7.reduce((sum, [, row]) => sum + row.total, 0);
  const paths: Record<string, number> = {};
  for (const row of Object.values(store.days)) {
    for (const [path, n] of Object.entries(row.paths)) paths[path] = (paths[path] ?? 0) + n;
  }
  const top = Object.entries(paths)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 12)
    .map(([path, count]) => ({ path, label: pathLabel(path), count }));
  return {
    since: store.since,
    total: store.total,
    today: todayRow.total,
    week,
    days: last7.map(([date, row]) => ({ date, total: row.total })),
    top,
    ready: store.total > 0 || Object.keys(store.days).length > 0,
  };
}
