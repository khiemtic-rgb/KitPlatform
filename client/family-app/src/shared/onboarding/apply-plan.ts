import {
  addCommitmentTemplate,
  createFamilyRoutine,
  ensureDayFlow,
  fetchFamilyRoutines,
  proposeScreenWallet,
  type FamilyRoutineDto,
} from '@/shared/api/family-os.api';
import {
  buildStarterPlan,
  suggestStarterWalletMinutes,
  type OnboardingAnswers,
  type StarterMission,
} from '@/shared/onboarding/onboarding';
import { syncSaveOnboarding } from '@/shared/value/value-sync';

function normTitle(t: string) {
  return t.trim().toLowerCase();
}

function pickTargetRoutine(routines: FamilyRoutineDto[]): FamilyRoutineDto | null {
  const active = routines.filter((r) => r.isActive);
  const daily =
    active.find((r) => /daily|ngày|sáng|nhịp/i.test(`${r.kind} ${r.displayName} ${r.code}`)) ??
    active[0] ??
    routines[0];
  return daily ?? null;
}

/** Apply starter missions onto family routine (add missing titles), then refresh day flow. */
export async function applyOnboardingPlan(input: {
  familyId: string;
  answers: OnboardingAnswers;
}): Promise<{ missions: StarterMission[]; routineId: string; added: number }> {
  const { familyId, answers } = input;
  const plan = buildStarterPlan(answers);
  const routines = await fetchFamilyRoutines(familyId);
  let routine = pickTargetRoutine(routines);
  let added = 0;

  if (!routine) {
    const created = await createFamilyRoutine(familyId, {
      code: `famixa_starter_${Date.now().toString(36)}`,
      displayName: `Famixa Starter · ${answers.childName}`,
      kind: 'daily',
      weekdays: [1, 2, 3, 4, 5, 6, 0],
      templates: plan.missions.map((m, i) => ({
        title: m.title,
        description: m.why,
        memberId: answers.childId,
        windowStart: m.windowStart,
        windowEnd: m.windowEnd,
        sortOrder: i + 1,
        priority: m.priority,
      })),
    });
    routine = created;
    added = plan.missions.length;
  } else {
    const existing = new Set(
      routine.templates
        .filter((t) => t.isActive)
        .map((t) => normTitle(t.title)),
    );
    let sortBase = routine.templates.reduce((m, t) => Math.max(m, t.sortOrder), 0);
    for (const mission of plan.missions) {
      if (existing.has(normTitle(mission.title))) continue;
      sortBase += 1;
      await addCommitmentTemplate(familyId, routine.id, {
        title: mission.title,
        description: mission.why,
        memberId: answers.childId,
        windowStart: mission.windowStart,
        windowEnd: mission.windowEnd,
        sortOrder: sortBase,
        priority: mission.priority,
      });
      existing.add(normTitle(mission.title));
      added += 1;
    }
  }

  // Force day flow rebuild/ensure so today picks up templates when possible
  await ensureDayFlow(familyId);

  // AFE: propose weekly screen wallet into Decision Inbox (parent must 👍 — never auto-apply)
  try {
    await proposeScreenWallet(familyId, {
      memberId: answers.childId,
      budgetMinutes: suggestStarterWalletMinutes(answers),
    });
  } catch {
    // Wallet propose is best-effort during setup
  }

  await syncSaveOnboarding(familyId, {
    ...answers,
    completedAt: new Date().toISOString(),
    missionTitles: plan.missions.map((m) => m.title),
  });

  return { missions: plan.missions, routineId: routine.id, added };
}

export async function skipOnboarding(familyId: string, partial?: Partial<OnboardingAnswers>) {
  await syncSaveOnboarding(familyId, {
    childId: partial?.childId ?? '',
    childName: partial?.childName ?? '',
    ageBand: partial?.ageBand ?? '7-9',
    struggles: partial?.struggles ?? [],
    goal: partial?.goal ?? 'fewer_nudges',
    completedAt: new Date().toISOString(),
    missionTitles: [],
    skipped: true,
  });
}
