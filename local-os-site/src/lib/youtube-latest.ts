export type LatestLongVideo = {
  id: string;
  title: string;
  author: string;
  channelUrl: string;
  watchUrl: string;
};

const cache = new Map<string, { at: number; video: LatestLongVideo | null }>();
const TTL_MS = 30 * 60 * 1000;

function uniq(ids: string[]) {
  const out: string[] = [];
  for (const id of ids) if (!out.includes(id)) out.push(id);
  return out;
}

/** Video dài mới nhất trên tab Videos. Bỏ Shorts (khung dọc). */
export async function latestLongVideo(handle: string): Promise<LatestLongVideo | null> {
  const key = handle.replace(/^@/, '').trim();
  if (!key) return null;
  const hit = cache.get(key);
  if (hit && Date.now() - hit.at < TTL_MS) return hit.video;

  let video: LatestLongVideo | null = null;
  try {
    const page = await fetch(`https://www.youtube.com/@${key}/videos`, {
      headers: { 'User-Agent': 'Mozilla/5.0', 'Accept-Language': 'vi,en;q=0.8' },
      signal: AbortSignal.timeout(8000),
    });
    if (page.ok) {
      const html = await page.text();
      const ids = uniq([...html.matchAll(/"videoId":"([A-Za-z0-9_-]{11})"/g)].map((m) => m[1])).slice(0, 6);
      for (const id of ids) {
        const oe = await fetch(
          `https://www.youtube.com/oembed?url=${encodeURIComponent(`https://www.youtube.com/watch?v=${id}`)}&format=json`,
          { signal: AbortSignal.timeout(5000) },
        );
        if (!oe.ok) continue;
        const data = (await oe.json()) as {
          title?: string;
          author_name?: string;
          author_url?: string;
          width?: number;
          height?: number;
        };
        if ((data.height ?? 0) > (data.width ?? 0)) continue;
        if (!data.title) continue;
        video = {
          id,
          title: data.title,
          author: data.author_name || key,
          channelUrl: data.author_url || `https://www.youtube.com/@${key}`,
          watchUrl: `https://www.youtube.com/watch?v=${id}`,
        };
        break;
      }
    }
  } catch {
    video = hit?.video ?? null;
  }

  cache.set(key, { at: Date.now(), video });
  return video;
}
