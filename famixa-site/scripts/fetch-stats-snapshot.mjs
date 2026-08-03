/**
 * Fetch Cloudflare zone analytics → public/stats-snapshot.json
 * Env: FAMIXA_CF_ZONE_ID; FAMIXA_CF_ANALYTICS_API_TOKEN (ưu tiên) | CF_ANALYTICS_API_TOKEN | CLOUDFLARE_API_TOKEN
 */
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  buildTodaySummary,
  mapDailyRows,
  mapHourlyRows,
  mergeDailyWithVnToday,
  vnDateString,
} from '../lib/stats-vn-today.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const outPath = join(root, 'public', 'stats-snapshot.json');
const ZONE_NAME = 'famixa.vn';

const STATS_QUERY = `
query FamixaStats($zoneTag: String!, $hStart: Time!, $hEnd: Time!, $dStart: Date!, $dEnd: Date!) {
  viewer {
    zones(filter: { zoneTag: $zoneTag }) {
      hours: httpRequests1hGroups(
        orderBy: [datetime_ASC]
        limit: 48
        filter: { datetime_geq: $hStart, datetime_lt: $hEnd }
      ) {
        dimensions { datetime }
        uniq { uniques }
        sum { requests, pageViews }
      }
      hourTotal: httpRequests1hGroups(
        limit: 1
        filter: { datetime_geq: $hStart, datetime_lt: $hEnd }
      ) {
        uniq { uniques }
        sum { requests, pageViews }
      }
      days: httpRequests1dGroups(
        orderBy: [date_ASC]
        limit: 7
        filter: { date_geq: $dStart, date_lt: $dEnd }
      ) {
        dimensions { date }
        uniq { uniques }
        sum { requests, pageViews }
      }
      topPages: httpRequestsAdaptiveGroups(
        limit: 15
        orderBy: [count_DESC]
        filter: {
          datetime_geq: $hStart
          datetime_lt: $hEnd
          requestSource: "eyeball"
        }
      ) {
        count
        dimensions { clientRequestPath }
      }
    }
  }
}
`;

function addDays(dateStr, days) {
  const [y, m, d] = dateStr.split('-').map(Number);
  const dt = new Date(Date.UTC(y, m - 1, d));
  dt.setUTCDate(dt.getUTCDate() + days);
  return dt.toISOString().slice(0, 10);
}

function writeSnapshot(payload) {
  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, `${JSON.stringify(payload, null, 2)}\n`, 'utf8');
}

async function resolveZoneId(token) {
  const fromEnv = process.env.FAMIXA_CF_ZONE_ID?.trim();
  if (fromEnv) return fromEnv;

  const res = await fetch(`https://api.cloudflare.com/client/v4/zones?name=${ZONE_NAME}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) {
    throw new Error(
      `Không list được zone ${ZONE_NAME} (HTTP ${res.status}). Thêm GitHub Secret FAMIXA_CF_ZONE_ID (Zone ID famixa.vn).`,
    );
  }
  const payload = await res.json();
  const zoneId = payload.result?.[0]?.id;
  if (!zoneId) {
    throw new Error(
      `Không tìm thấy zone ${ZONE_NAME}. Thêm GitHub Secret FAMIXA_CF_ZONE_ID (Zone ID famixa.vn).`,
    );
  }
  console.log('fetch-stats-snapshot: zone', zoneId);
  return zoneId;
}

async function fetchStats() {
  const token =
    process.env.FAMIXA_CF_ANALYTICS_API_TOKEN?.trim() ||
    process.env.CF_ANALYTICS_API_TOKEN?.trim() ||
    process.env.CLOUDFLARE_API_TOKEN?.trim();

  if (!token) {
    console.error(
      'fetch-stats-snapshot: missing token — set FAMIXA_CF_ANALYTICS_API_TOKEN (Analytics Read, zone famixa.vn)',
    );
    process.exit(1);
  }

  const zoneId = await resolveZoneId(token);

  const now = new Date();
  const hStart = new Date(now.getTime() - 24 * 60 * 60 * 1000);
  const today = vnDateString(now);
  const dStart = addDays(today, -6);
  const dEnd = addDays(today, 1);

  const res = await fetch('https://api.cloudflare.com/client/v4/graphql', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      query: STATS_QUERY,
      variables: {
        zoneTag: zoneId,
        hStart: hStart.toISOString(),
        hEnd: now.toISOString(),
        dStart,
        dEnd,
      },
    }),
  });

  if (!res.ok) {
    writeSnapshot({
      ok: false,
      error: `Cloudflare API HTTP ${res.status}`,
      generatedAt: now.toISOString(),
    });
    console.warn(`fetch-stats-snapshot: HTTP ${res.status}`);
    process.exit(1);
  }

  const payload = await res.json();
  if (payload.errors?.length) {
    const msg = payload.errors.map((e) => e.message).join('; ');
    const hint = msg.includes('analytics.read')
      ? ' Token thiếu Zone Analytics Read cho famixa.vn — tạo FAMIXA_CF_ANALYTICS_API_TOKEN riêng hoặc sửa token Cloudflare.'
      : '';
    writeSnapshot({
      ok: false,
      error: msg + hint,
      generatedAt: now.toISOString(),
    });
    console.error('fetch-stats-snapshot GraphQL:', msg + hint);
    process.exit(1);
  }

  const zone = payload.data?.viewer?.zones?.[0];
  if (!zone) {
    writeSnapshot({
      ok: false,
      error: 'Không tìm thấy zone — kiểm tra token Analytics Read + FAMIXA_CF_ZONE_ID.',
      generatedAt: now.toISOString(),
    });
    process.exit(1);
  }

  const hourTotal = zone.hourTotal?.[0];
  const hourly = mapHourlyRows(zone.hours);
  const daily = mapDailyRows(zone.days);
  const todaySummary = buildTodaySummary(hourly, daily, today);
  const dailyWithToday = mergeDailyWithVnToday(daily, hourly, today);

  writeSnapshot({
    ok: true,
    generatedAt: now.toISOString(),
    timezone: 'Asia/Ho_Chi_Minh',
    summary: {
      ...todaySummary,
      last24hVisitors: hourTotal?.uniq?.uniques ?? 0,
      last24hPageViews: hourTotal?.sum?.pageViews ?? 0,
      last24hRequests: hourTotal?.sum?.requests ?? 0,
    },
    hourly: hourly.map(({ time, visitors, requests }) => ({ time, visitors, requests })),
    daily: dailyWithToday,
    topPages: (zone.topPages ?? []).map((row) => ({
      path: row.dimensions?.clientRequestPath ?? '/',
      views: row.count ?? 0,
    })),
  });
  console.log('fetch-stats-snapshot: wrote', outPath);
}

fetchStats().catch((err) => {
  writeSnapshot({
    ok: false,
    error: err instanceof Error ? err.message : 'Unknown error',
    generatedAt: new Date().toISOString(),
  });
  console.error('fetch-stats-snapshot failed:', err);
  process.exit(1);
});
