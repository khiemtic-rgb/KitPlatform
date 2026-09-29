const KEY = 'novixa.reports.recent.v1';
const MAX = 8;

export type RecentReport = {
  path: string;
  code: string;
  name: string;
  group: string;
  openedBy: string;
  openedAt: string;
  from?: string;
  to?: string;
};

export function readRecentReports(): RecentReport[] {
  try {
    const raw = localStorage.getItem(KEY);
    const parsed = raw ? (JSON.parse(raw) as RecentReport[]) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function pushRecentReport(entry: RecentReport) {
  const next = [entry, ...readRecentReports().filter((item) => item.path !== entry.path)].slice(0, MAX);
  localStorage.setItem(KEY, JSON.stringify(next));
}
