import {
  deleteFamilyOnboarding,
  fetchFamilyValueState,
  incrementFamilyNudge,
  putFamilyHealthScore,
  putFamilyNudgeCount,
  putFamilyOnboarding,
} from '@/shared/api/family-os.api';
import {
  getOnboardingProfile,
  saveOnboardingProfileLocal,
  type OnboardingProfile,
} from '@/shared/onboarding/onboarding';
import {
  getHealthScoreOn,
  listLocalHealthScores,
  mergeHealthScores,
  persistHealthScoreLocal,
} from '@/shared/value/family-health-score';
import {
  getNudgeCount,
  listLocalNudgeCounts,
  mergeNudgeCounts,
  setNudgeCountLocal,
} from '@/shared/nudge/nudge-stats';

const hydrated = new Set<string>();

function quiet(err: unknown) {
  if (import.meta.env.DEV) console.warn('[value-sync]', err);
}

/** Pull server value state into local cache; push local-only wins back (max merge). */
export async function hydrateFamilyValueState(familyId: string, force = false): Promise<void> {
  if (!familyId) return;
  if (!force && hydrated.has(familyId)) return;

  try {
    const remote = await fetchFamilyValueState(familyId);
    const localScores = listLocalHealthScores(familyId);
    const localNudges = listLocalNudgeCounts(familyId);

    const mergedScores: Record<string, number> = { ...localScores };
    for (const [date, score] of Object.entries(remote.healthScores)) {
      mergedScores[date] = Math.max(Number(mergedScores[date] ?? 0), score);
    }
    mergeHealthScores(familyId, mergedScores);

    const mergedNudges: Record<string, number> = { ...localNudges };
    for (const [date, count] of Object.entries(remote.nudgeCounts)) {
      mergedNudges[date] = Math.max(Number(mergedNudges[date] ?? 0), count);
    }
    mergeNudgeCounts(familyId, mergedNudges);

    if (remote.onboarding?.payloadJson) {
      try {
        const parsed = JSON.parse(remote.onboarding.payloadJson) as OnboardingProfile;
        if (parsed?.completedAt) {
          saveOnboardingProfileLocal(familyId, {
            ...parsed,
            completedAt: parsed.completedAt || remote.onboarding.completedAt,
          });
        }
      } catch {
        /* ignore bad payload */
      }
    }

    // Push local days that are ahead of server (first migration / offline catch-up)
    const pushScores: Promise<unknown>[] = [];
    for (const [date, score] of Object.entries(mergedScores)) {
      if (score > Number(remote.healthScores[date] ?? -1)) {
        pushScores.push(putFamilyHealthScore(familyId, date, score));
      }
    }
    const pushNudges: Promise<unknown>[] = [];
    for (const [date, count] of Object.entries(mergedNudges)) {
      if (count > Number(remote.nudgeCounts[date] ?? -1)) {
        pushNudges.push(putFamilyNudgeCount(familyId, date, count));
      }
    }

    const localOnboard = getOnboardingProfile(familyId);
    if (localOnboard?.completedAt && !remote.onboarding) {
      pushScores.push(
        putFamilyOnboarding(familyId, JSON.stringify(localOnboard), localOnboard.completedAt),
      );
    }

    await Promise.allSettled([...pushScores, ...pushNudges]);
    hydrated.add(familyId);
  } catch (err) {
    quiet(err);
  }
}

export function syncHealthScore(familyId: string, flowDate: string, score: number) {
  const prev = getHealthScoreOn(familyId, flowDate);
  if (prev === score) return;
  persistHealthScoreLocal(familyId, flowDate, score);
  void putFamilyHealthScore(familyId, flowDate, score).catch(quiet);
}

export function syncRecordNudge(familyId: string, flowDate: string, count = 1): number {
  const localNext = getNudgeCount(familyId, flowDate) + Math.max(1, count);
  setNudgeCountLocal(familyId, flowDate, localNext);
  void incrementFamilyNudge(familyId, flowDate, Math.max(1, count))
    .then((serverCount) => {
      if (serverCount > localNext) setNudgeCountLocal(familyId, flowDate, serverCount);
    })
    .catch(quiet);
  return localNext;
}

export async function syncSaveOnboarding(familyId: string, profile: OnboardingProfile) {
  saveOnboardingProfileLocal(familyId, profile);
  await putFamilyOnboarding(familyId, JSON.stringify(profile), profile.completedAt);
}

export async function syncClearOnboarding(familyId: string) {
  const { clearOnboardingProfileLocal } = await import('@/shared/onboarding/onboarding');
  clearOnboardingProfileLocal(familyId);
  hydrated.delete(familyId);
  await deleteFamilyOnboarding(familyId);
}

/** Expose last known local score for delta math after hydrate. */
export function peekHealthScore(familyId: string, flowDate: string): number | null {
  return getHealthScoreOn(familyId, flowDate);
}
