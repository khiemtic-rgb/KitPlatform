import { useAuthStore } from '@/shared/auth/auth.store';

/** Care layer (guest / prospect): no pharmacy member required. */
export const CARE_FAMILY_MAX_WITHOUT_MEMBER = 2;
export const CARE_AI_DAILY_LIMIT_WITHOUT_MEMBER = 5;

export function isPharmacyMemberRelation(pharmacyRelation?: string | null): boolean {
  return (pharmacyRelation ?? '').trim().toLowerCase() === 'member';
}

/** True when server says member (commerce unlocked). Guests / prospect = care-only. */
export function isPharmacyMemberFromProfile(): boolean {
  return isPharmacyMemberRelation(useAuthStore.getState().profile?.pharmacyRelation);
}

function aiUsageKey(customerId: string | undefined): string {
  const day = new Date().toISOString().slice(0, 10);
  return `novixa_ai_asks_${customerId || 'guest'}_${day}`;
}

export function getAiAsksUsedToday(customerId?: string | null): number {
  if (typeof window === 'undefined') return 0;
  const raw = window.localStorage.getItem(aiUsageKey(customerId ?? undefined));
  const n = Number(raw);
  return Number.isFinite(n) && n > 0 ? Math.floor(n) : 0;
}

export function recordAiAskToday(customerId?: string | null): number {
  if (typeof window === 'undefined') return 0;
  const key = aiUsageKey(customerId ?? undefined);
  const next = getAiAsksUsedToday(customerId) + 1;
  window.localStorage.setItem(key, String(next));
  return next;
}

export function aiAsksRemainingToday(customerId?: string | null, isMember?: boolean): number | null {
  if (isMember) return null;
  return Math.max(0, CARE_AI_DAILY_LIMIT_WITHOUT_MEMBER - getAiAsksUsedToday(customerId));
}
