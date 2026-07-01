const MS_DAY = 86_400_000;
const VN_OFFSET_MS = 7 * 3_600_000;

/** Start of today in Vietnam (UTC instant). */
export function vnTodayStartUtcMs(): number {
  const vn = Date.now() + VN_OFFSET_MS;
  const d = new Date(vn);
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()) - VN_OFFSET_MS;
}

export function rollingDaysRangeIso(daysIncludingToday: number): { from: string; to: string } {
  const safeDays = Math.max(1, daysIncludingToday);
  const todayStart = vnTodayStartUtcMs();
  const todayEnd = todayStart + MS_DAY;
  const from = todayStart - (safeDays - 1) * MS_DAY;
  return {
    from: new Date(from).toISOString(),
    to: new Date(todayEnd).toISOString(),
  };
}

export function formatVnDayLabel(utcMs: number): string {
  const vn = new Date(utcMs + VN_OFFSET_MS);
  const dd = String(vn.getUTCDate()).padStart(2, '0');
  const mm = String(vn.getUTCMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}`;
}

/** Full VN date key — matches SALES-01 periodLabel (dd/MM/yyyy). */
export function formatVnDayKey(utcMs: number): string {
  const vn = new Date(utcMs + VN_OFFSET_MS);
  const dd = String(vn.getUTCDate()).padStart(2, '0');
  const mm = String(vn.getUTCMonth() + 1).padStart(2, '0');
  const yyyy = vn.getUTCFullYear();
  return `${dd}/${mm}/${yyyy}`;
}

export function normalizeReportPeriodLabel(label: string): string {
  const parts = label.trim().split('/');
  if (parts.length >= 3) {
    return `${parts[0].padStart(2, '0')}/${parts[1].padStart(2, '0')}/${parts[2]}`;
  }
  if (parts.length === 2) {
    return `${parts[0].padStart(2, '0')}/${parts[1].padStart(2, '0')}`;
  }
  return label.trim();
}

function readRowNumber(row: Record<string, unknown>, key: string): number {
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  return Number(row[key] ?? row[pascal] ?? 0);
}

export function readReportFieldNumber(row: Record<string, unknown>, key: string): number {
  return readRowNumber(row, key);
}

export function readReportFieldString(row: Record<string, unknown>, key: string): string {
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  return String(row[key] ?? row[pascal] ?? '').trim();
}

export type RevenuePeriodDays = 7 | 14 | 30;

function readRowLabel(row: Record<string, unknown>): string {
  const raw = String(row.periodLabel ?? row.PeriodLabel ?? '');
  return normalizeReportPeriodLabel(raw);
}

export type RevenueChartPoint = {
  label: string;
  netAmount: number;
};

export function buildDailyRevenueChartPoints(
  daysIncludingToday: number,
  rows: Record<string, unknown>[],
): RevenueChartPoint[] {
  const safeDays = Math.max(1, daysIncludingToday);
  const todayStart = vnTodayStartUtcMs();
  const byKey = new Map<string, number>();
  for (const row of rows) {
    byKey.set(readRowLabel(row), readRowNumber(row, 'netAmount'));
  }

  const points: RevenueChartPoint[] = [];
  for (let offset = safeDays - 1; offset >= 0; offset -= 1) {
    const dayStart = todayStart - offset * MS_DAY;
    const label = formatVnDayLabel(dayStart);
    const key = formatVnDayKey(dayStart);
    points.push({ label, netAmount: byKey.get(key) ?? byKey.get(label) ?? 0 });
  }
  return points;
}

export function sumNetAmount(points: RevenueChartPoint[]): number {
  return points.reduce((sum, point) => sum + point.netAmount, 0);
}
