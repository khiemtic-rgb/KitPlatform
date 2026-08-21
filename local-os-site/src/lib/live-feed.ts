export type PublicFeed = {
  listings?: Array<Record<string, unknown> & { id: string; kind: string }>;
  groups?: Array<Record<string, unknown> & { id: string; name: string; url: string }>;
  exportedAt?: string | null;
};

const KEY = 'feed:public';
const TTL_MS = 4000;

type Kv = { get: (k: string) => Promise<string | null>; put: (k: string, v: string) => Promise<void> };
type FeedEnv = { HITS?: Kv; FEED_SYNC_TOKEN?: string };

let memory: { at: number; feed: PublicFeed } | null = null;

async function env(): Promise<FeedEnv | null> {
  try {
    const mod = await import('cloudflare:workers');
    return ((mod as { env?: FeedEnv }).env) ?? null;
  } catch {
    return null;
  }
}

export async function readLiveFeed(): Promise<PublicFeed | null> {
  if (memory && Date.now() - memory.at < TTL_MS) return memory.feed;
  const ns = (await env())?.HITS;
  if (!ns) return null;
  const raw = await ns.get(KEY);
  if (!raw) return null;
  try {
    const feed = JSON.parse(raw) as PublicFeed;
    if (!Array.isArray(feed.listings)) return null;
    memory = { at: Date.now(), feed };
    return feed;
  } catch {
    return null;
  }
}

export async function writeLiveFeed(feed: PublicFeed): Promise<number> {
  const ns = (await env())?.HITS;
  if (!ns) throw new Error('KV chưa sẵn sàng.');
  const listings = Array.isArray(feed.listings) ? feed.listings : [];
  const jobs = listings.filter((item) => item.kind === 'job').length;
  if (listings.length === 0 || jobs < 8) {
    throw new Error(`Không ghi feed thiếu việc (${jobs}).`);
  }
  const next: PublicFeed = {
    listings,
    groups: Array.isArray(feed.groups) ? feed.groups : [],
    exportedAt: feed.exportedAt ?? new Date().toISOString(),
  };
  await ns.put(KEY, JSON.stringify(next));
  memory = { at: Date.now(), feed: next };
  return listings.length;
}

export async function feedToken(): Promise<string> {
  return ((await env())?.FEED_SYNC_TOKEN ?? 'tnl-kv-feed-7c4e91b2a8d64f0e').trim();
}
