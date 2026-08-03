// Cloudflare daily buckets use UTC; VN calendar day needs hourly rollup after midnight +7.

export function vnDateString(date = new Date()) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(date);
}

export function vnDateOfIso(isoTime) {
  return vnDateString(new Date(isoTime));
}

export function mapHourlyRows(hours) {
  return (hours ?? []).map((row) => ({
    time: row.dimensions?.datetime ?? '',
    visitors: row.uniq?.uniques ?? 0,
    requests: row.sum?.requests ?? 0,
    pageViews: row.sum?.pageViews ?? 0,
  }));
}

export function mapDailyRows(days) {
  return (days ?? []).map((row) => ({
    date: row.dimensions?.date ?? '',
    visitors: row.uniq?.uniques ?? 0,
    pageViews: row.sum?.pageViews ?? 0,
    requests: row.sum?.requests ?? 0,
  }));
}

export function aggregateVnToday(hourly, todayVn) {
  const rows = hourly.filter((row) => row.time && vnDateOfIso(row.time) === todayVn);
  return {
    date: todayVn,
    visitors: rows.reduce((sum, row) => sum + row.visitors, 0),
    pageViews: rows.reduce((sum, row) => sum + row.pageViews, 0),
    requests: rows.reduce((sum, row) => sum + row.requests, 0),
  };
}

export function buildTodaySummary(hourly, daily, todayVn) {
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

export function mergeDailyWithVnToday(daily, hourly, todayVn) {
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
