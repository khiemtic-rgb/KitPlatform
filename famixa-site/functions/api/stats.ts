type CfEnv = {
  STATS_VIEW_KEY?: string;
  CF_ZONE_ID?: string;
  CLOUDFLARE_API_TOKEN?: string;
  FAMIXA_CF_ZONE_ID?: string;
  FAMIXA_CF_ANALYTICS_API_TOKEN?: string;
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

type StatsHourRow = {
  time: string;
  visitors: number;
  requests: number;
  pageViews: number;
};

type StatsDayRow = {
  date: string;
  visitors: number;
  pageViews: number;
  requests: number;
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

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store',
    },
  });
}

function vnDateString(date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(date);
}

function vnDateOfIso(isoTime: string): string {
  return vnDateString(new Date(isoTime));
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

function mapHourlyRows(
  hours?: Array<{
    dimensions?: { datetime?: string };
    uniq?: { uniques?: number };
    sum?: { requests?: number; pageViews?: number };
  }>,
): StatsHourRow[] {
  return (hours ?? []).map((row) => ({
    time: row.dimensions?.datetime ?? '',
    visitors: row.uniq?.uniques ?? 0,
    requests: row.sum?.requests ?? 0,
    pageViews: row.sum?.pageViews ?? 0,
  }));
}

function mapDailyRows(
  days?: Array<{
    dimensions?: { date?: string };
    uniq?: { uniques?: number };
    sum?: { requests?: number; pageViews?: number };
  }>,
): StatsDayRow[] {
  return (days ?? []).map((row) => ({
    date: row.dimensions?.date ?? '',
    visitors: row.uniq?.uniques ?? 0,
    pageViews: row.sum?.pageViews ?? 0,
    requests: row.sum?.requests ?? 0,
  }));
}

function aggregateVnToday(hourly: StatsHourRow[], todayVn: string): StatsDayRow {
  const rows = hourly.filter((row) => row.time && vnDateOfIso(row.time) === todayVn);
  return {
    date: todayVn,
    visitors: rows.reduce((sum, row) => sum + row.visitors, 0),
    pageViews: rows.reduce((sum, row) => sum + row.pageViews, 0),
    requests: rows.reduce((sum, row) => sum + row.requests, 0),
  };
}

function buildTodaySummary(hourly: StatsHourRow[], daily: StatsDayRow[], todayVn: string) {
  const dailyToday = daily.find((row) => row.date === todayVn);
  const fromHours = aggregateVnToday(hourly, todayVn);

  if (dailyToday && (dailyToday.requests > 0 || dailyToday.visitors > 0)) {
    return {
      todayVisitors: dailyToday.visitors,
      todayPageViews: dailyToday.pageViews,
      todayRequests: dailyToday.requests,
    };
  }

  return {
    todayVisitors: fromHours.visitors,
    todayPageViews: fromHours.pageViews,
    todayRequests: fromHours.requests,
  };
}

function mergeDailyWithVnToday(
  daily: StatsDayRow[],
  hourly: StatsHourRow[],
  todayVn: string,
): StatsDayRow[] {
  const fromHours = aggregateVnToday(hourly, todayVn);
  const withoutToday = daily.filter((row) => row.date !== todayVn);
  const merged = [...withoutToday];

  if (fromHours.requests > 0 || fromHours.visitors > 0 || fromHours.pageViews > 0) {
    merged.push(fromHours);
  } else {
    const dailyToday = daily.find((row) => row.date === todayVn);
    if (dailyToday) merged.push(dailyToday);
  }

  merged.sort((a, b) => a.date.localeCompare(b.date));
  return merged.slice(-7);
}

function readAuth(request: Request): string | null {
  const header = request.headers.get('Authorization');
  if (header?.startsWith('Bearer ')) return header.slice(7).trim();
  return new URL(request.url).searchParams.get('key')?.trim() ?? null;
}

function resolveZoneId(env: CfEnv): string | undefined {
  return env.FAMIXA_CF_ZONE_ID?.trim() || env.CF_ZONE_ID?.trim();
}

function resolveAnalyticsToken(env: CfEnv): string | undefined {
  return env.FAMIXA_CF_ANALYTICS_API_TOKEN?.trim() || env.CLOUDFLARE_API_TOKEN?.trim();
}

async function fetchZoneStats(env: CfEnv) {
  const zoneId = resolveZoneId(env);
  const token = resolveAnalyticsToken(env);
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
    hourly: hourly.map((row) => ({
      time: row.time,
      visitors: row.visitors,
      requests: row.requests,
    })),
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
    CF_ZONE_ID: Boolean(resolveZoneId(env)),
    CLOUDFLARE_API_TOKEN: Boolean(resolveAnalyticsToken(env)),
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
          error: 'Chưa cấu hình zone/token Analytics trên Cloudflare Pages.',
          configured: envStatus(context.env),
          hint: 'Thêm FAMIXA_CF_ZONE_ID + FAMIXA_CF_ANALYTICS_API_TOKEN (hoặc CF_ZONE_ID + CLOUDFLARE_API_TOKEN), rồi Retry deployment.',
        },
        503,
      );
    }
    return json({ error: `Không lấy được dữ liệu Cloudflare: ${message}` }, 502);
  }
};
