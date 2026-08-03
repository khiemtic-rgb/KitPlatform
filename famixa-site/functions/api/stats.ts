type CfEnv = {
  STATS_VIEW_KEY?: string;
  CF_ZONE_ID?: string;
  CLOUDFLARE_API_TOKEN?: string;
};

type GraphQlResponse = {
  data?: {
    viewer?: {
      zones?: Array<{
        hours?: Array<{
          dimensions?: { datetime?: string };
          uniq?: { uniques?: number };
          sum?: { requests?: number; pageViews?: number };
        }>;
        hourTotal?: Array<{
          uniq?: { uniques?: number };
          sum?: { requests?: number; pageViews?: number };
        }>;
        days?: Array<{
          dimensions?: { date?: string };
          uniq?: { uniques?: number };
          sum?: { requests?: number; pageViews?: number };
        }>;
        topPages?: Array<{
          count?: number;
          dimensions?: { clientRequestPath?: string };
        }>;
      }>;
    };
  };
  errors?: Array<{ message?: string }>;
};

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
        limit: 10
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

import {
  buildTodaySummary,
  mapDailyRows,
  mapHourlyRows,
  mergeDailyWithVnToday,
  vnDateString,
} from '../../lib/stats-vn-today';

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store',
    },
  });
}

function addDays(dateStr: string, days: number): string {
  const [y, m, d] = dateStr.split('-').map(Number);
  const dt = new Date(Date.UTC(y, m - 1, d));
  dt.setUTCDate(dt.getUTCDate() + days);
  return dt.toISOString().slice(0, 10);
}

function toIsoTime(date: Date): string {
  return date.toISOString();
}

function readAuth(request: Request): string | null {
  const header = request.headers.get('Authorization');
  if (header?.startsWith('Bearer ')) return header.slice(7).trim();
  return new URL(request.url).searchParams.get('key')?.trim() ?? null;
}

async function fetchZoneStats(env: CfEnv) {
  const zoneId = env.CF_ZONE_ID?.trim();
  const token = env.CLOUDFLARE_API_TOKEN?.trim();
  if (!zoneId || !token) {
    throw new Error('MISSING_CONFIG');
  }

  const now = new Date();
  const hStart = new Date(now.getTime() - 24 * 60 * 60 * 1000);
  const today = vnDateString(now);
  const dStart = addDays(today, -6);
  const dEnd = addDays(today, 1);

  const response = await fetch('https://api.cloudflare.com/client/v4/graphql', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      query: STATS_QUERY,
      variables: {
        zoneTag: zoneId,
        hStart: toIsoTime(hStart),
        hEnd: toIsoTime(now),
        dStart,
        dEnd,
      },
    }),
  });

  if (!response.ok) {
    throw new Error(`CF_HTTP_${response.status}`);
  }

  const payload = (await response.json()) as GraphQlResponse;
  if (payload.errors?.length) {
    throw new Error(payload.errors.map((e) => e.message).join('; '));
  }

  const zone = payload.data?.viewer?.zones?.[0];
  if (!zone) {
    throw new Error('ZONE_NOT_FOUND');
  }

  const hourTotal = zone.hourTotal?.[0];
  const hourly = mapHourlyRows(zone.hours);
  const daily = mapDailyRows(zone.days);
  const todaySummary = buildTodaySummary(hourly, daily, today);
  const dailyWithToday = mergeDailyWithVnToday(daily, hourly, today);

  return {
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
  };
}

function envStatus(env: CfEnv) {
  return {
    STATS_VIEW_KEY: Boolean(env.STATS_VIEW_KEY?.trim()),
    CF_ZONE_ID: Boolean(env.CF_ZONE_ID?.trim()),
    CLOUDFLARE_API_TOKEN: Boolean(env.CLOUDFLARE_API_TOKEN?.trim()),
  };
}

export const onRequestGet: PagesFunction<CfEnv> = async (context) => {
  const expected = context.env.STATS_VIEW_KEY?.trim();
  const auth = readAuth(context.request);
  const configured = envStatus(context.env);

  if (!expected) {
    return json(
      {
        error: 'Chưa cấu hình STATS_VIEW_KEY trên Cloudflare Pages.',
        configured,
        hint:
          'Workers & Pages → famixa → Settings → Variables and secrets: thêm STATS_VIEW_KEY (= famixa2026), tick Production, Save. Sau đó Deployments → Retry deployment.',
      },
      503,
    );
  }

  if (!auth || auth !== expected) {
    return json({ error: 'Mật khẩu không đúng.' }, 401);
  }

  try {
    const stats = await fetchZoneStats(context.env);
    return json(stats);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'UNKNOWN';
    if (message === 'MISSING_CONFIG') {
      return json(
        {
          error:
            'Chưa cấu hình CF_ZONE_ID hoặc CLOUDFLARE_API_TOKEN trên Cloudflare Pages.',
          configured: envStatus(context.env),
          hint: 'Thêm CF_ZONE_ID (zone famixa.vn) và CLOUDFLARE_API_TOKEN (Analytics Read), rồi Retry deployment.',
        },
        503,
      );
    }
    return json({ error: `Không lấy được dữ liệu Cloudflare: ${message}` }, 502);
  }
};
