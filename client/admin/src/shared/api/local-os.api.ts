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
