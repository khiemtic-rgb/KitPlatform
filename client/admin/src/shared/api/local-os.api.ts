import { http } from '@/shared/api/http';

export type LocalListing = {
  id: string;
  kind: string;
  title: string;
  summary?: string | null;
  organizationName?: string | null;
  placeText?: string | null;
  contactPhone?: string | null;
  contactName?: string | null;
  salaryText?: string | null;
  workingTime?: string | null;
  employmentType?: string | null;
  category?: string | null;
  requirements?: string | null;
  startAt?: string | null;
  endAt?: string | null;
  priceMonth?: number | null;
  roomType?: string | null;
  trust: string;
  safetyFlag: boolean;
  status: string;
  sourceKind?: string | null;
  sourceUrl?: string | null;
  sourceId?: string | null;
  sourceName?: string | null;
  publishedAt?: string | null;
  lastCheckedAt?: string | null;
  expiresAt?: string | null;
};

export async function fetchLocalOsListings(params?: {
  kind?: string;
  q?: string;
  status?: string;
}): Promise<LocalListing[]> {
  const { data } = await http.get<LocalListing[]>('/local-os/listings', { params });
  return data;
}

export async function setLocalOsListingStatus(
  id: string,
  status: string,
): Promise<LocalListing> {
  const { data } = await http.post<LocalListing>(`/local-os/listings/${id}/status`, { status });
  return data;
}

export type LocalSource = {
  id: string;
  sourceKind: string;
  name: string;
  url?: string | null;
  status: string;
  platform: string;
  category: string;
  audience: string;
  geo: string;
  notes?: string | null;
  watchEnabled?: boolean;
  lastWatchedAt?: string | null;
};

export type LocalWatchRun = {
  id: string;
  startedAt: string;
  finishedAt?: string | null;
  trigger: string;
  sourcesScanned: number;
  linksSeen: number;
  createdCount: number;
  skippedExisting: number;
  skippedFilter: number;
  errorCount: number;
  note?: string | null;
};

export async function fetchLocalOsSources(): Promise<LocalSource[]> {
  const { data } = await http.get<LocalSource[]>('/local-os/sources');
  return data;
}

export async function createLocalOsSource(body: {
  sourceKind: string;
  name: string;
  url?: string;
  status?: string;
  platform?: string;
  category?: string;
  audience?: string;
  geo?: string;
  notes?: string;
}): Promise<LocalSource> {
  const { data } = await http.post<LocalSource>('/local-os/sources', body);
  return data;
}

export async function updateLocalOsSource(
  id: string,
  body: {
    sourceKind: string;
    name: string;
    url?: string;
    status?: string;
    category?: string;
    audience?: string;
    notes?: string;
  },
): Promise<LocalSource> {
  const { data } = await http.put<LocalSource>(`/local-os/sources/${id}`, body);
  return data;
}

export async function setLocalOsSourceStatus(id: string, status: string): Promise<LocalSource> {
  const { data } = await http.post<LocalSource>(`/local-os/sources/${id}/status`, { status });
  return data;
}

export async function fetchLocalOsWatchRuns(): Promise<LocalWatchRun[]> {
  const { data } = await http.get<LocalWatchRun[]>('/local-os/watch/runs');
  return data;
}

export async function runLocalOsWatch(): Promise<LocalWatchRun> {
  const { data } = await http.post<LocalWatchRun>('/local-os/watch/run', {}, { timeout: 15_000 });
  return data;
}

export async function rewriteLocalOsListing(body: {
  text: string;
  kind?: string;
}): Promise<{
  title: string;
  body: string;
  place?: string | null;
  phone?: string | null;
  salary?: string | null;
  via: string;
  note?: string | null;
}> {
  const { data } = await http.post<{
    title: string;
    body: string;
    place?: string | null;
    phone?: string | null;
    salary?: string | null;
    via: string;
    note?: string | null;
  }>('/local-os/listings/rewrite', body, { timeout: 50_000 });
  return data;
}

export async function ingestLocalOsSource(body: {
  sourceUrl?: string;
  pastedText?: string;
  kind?: string;
  sourceId?: string;
}): Promise<{ listing: LocalListing; note: string; existing: boolean }> {
  const { data } = await http.post<{ listing: LocalListing; note: string; existing: boolean }>(
    '/local-os/ingest',
    body,
  );
  return data;
}

export async function createLocalOsListing(body: {
  kind: string;
  title: string;
  summary?: string;
  placeText?: string;
  salaryText?: string;
  contactPhone?: string;
  contactName?: string;
  workingTime?: string;
  requirements?: string;
  status?: string;
}): Promise<LocalListing> {
  const { data } = await http.post<LocalListing>('/local-os/listings', {
    ...body,
    sourceKind: 'admin_write',
    trust: 'UNVERIFIED',
    status: body.status ?? 'NEEDS_REVIEW',
  });
  return data;
}

export type LocalListingReport = {
  id: string;
  listingId: string;
  reason: string;
  note?: string | null;
  createdAt: string;
  listingTitle?: string | null;
  listingKind?: string | null;
  listingStatus?: string | null;
};

export async function fetchLocalOsReports(): Promise<LocalListingReport[]> {
  const { data } = await http.get<LocalListingReport[]>('/local-os/reports');
  return data;
}

export async function publishLocalOsHomepage(): Promise<{
  ok: boolean;
  message: string;
  listingCount: number;
  skipped?: boolean;
}> {
  const { data } = await http.post<{
    ok: boolean;
    message: string;
    listingCount: number;
    skipped?: boolean;
  }>('/local-os/feed/publish', {}, { timeout: 45_000 });
  return data;
}

export async function updateLocalOsListing(
  id: string,
  body: Partial<LocalListing> & { kind: string; title: string },
): Promise<LocalListing> {
  const { data } = await http.put<LocalListing>(`/local-os/listings/${id}`, body);
  return data;
}
