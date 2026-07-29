export type StatsSummary = {
  todayVisitors: number;
  todayPageViews: number;
  todayRequests: number;
  last24hVisitors: number;
  last24hPageViews: number;
  last24hRequests: number;
};

export type StatsHourly = {
  time: string;
  visitors: number;
  requests: number;
};

export type StatsDaily = {
  date: string;
  visitors: number;
  pageViews: number;
  requests: number;
};

export type StatsTopPage = {
  path: string;
  views: number;
};

export type StatsPayload = {
  ok?: boolean;
  error?: string;
  generatedAt: string;
  timezone?: string;
  summary: StatsSummary;
  hourly: StatsHourly[];
  daily: StatsDaily[];
  topPages: StatsTopPage[];
};
