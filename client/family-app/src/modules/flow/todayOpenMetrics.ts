/**
 * Client-only loop metrics for Today Open Stack (warmth / pending / ritual).
 * Stored locally per day for pilot review — not sent to server yet.
 */
export type TodayOpenMetricEvent =
  | 'warmth_shown'
  | 'warmth_dismiss'
  | 'pending_tap'
  | 'seen_shown'
  | 'ritual_done'
  | 'memory_yarn_shown'
  | 'memory_yarn_open'
  | 'ack_voice';

type DayBucket = {
  date: string;
  counts: Partial<Record<TodayOpenMetricEvent, number>>;
  lastAt: Partial<Record<TodayOpenMetricEvent, string>>;
};

const STORAGE_KEY = 'famixa.open.metrics.v1';

function todayKey(d = new Date()): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function readBucket(): DayBucket {
  const date = todayKey();
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { date, counts: {}, lastAt: {} };
    const parsed = JSON.parse(raw) as DayBucket;
    if (parsed?.date !== date) return { date, counts: {}, lastAt: {} };
    return {
      date,
      counts: parsed.counts ?? {},
      lastAt: parsed.lastAt ?? {},
    };
  } catch {
    return { date, counts: {}, lastAt: {} };
  }
}

function writeBucket(bucket: DayBucket): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(bucket));
  } catch {
    /* ignore */
  }
}

/** Fire-and-forget local counter + optional console breadcrumb. */
export function trackTodayOpen(
  event: TodayOpenMetricEvent,
  props?: Record<string, string | number | boolean | undefined>,
): void {
  const bucket = readBucket();
  bucket.counts[event] = (bucket.counts[event] ?? 0) + 1;
  bucket.lastAt[event] = new Date().toISOString();
  writeBucket(bucket);
  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.debug('[today-open]', event, props ?? {}, bucket.counts);
  }
}

export function readTodayOpenMetrics(): DayBucket {
  return readBucket();
}