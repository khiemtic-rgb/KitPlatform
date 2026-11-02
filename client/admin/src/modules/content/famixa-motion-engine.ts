/** HOW picker at generate time. Credentials stay on Cấu hình AI. */

export const FAMIXA_MOTION_ENGINE_KEY = 'kit.famixaSeries.engine';

export type FamixaMotionEngine = 'turbo' | 'wan' | 'veo';

export const FAMIXA_MOTION_ENGINE_OPTIONS: { value: FamixaMotionEngine; label: string }[] = [
  { value: 'turbo', label: 'Runway · Gen-4 Turbo' },
  { value: 'wan', label: 'Wan 2.1 · Fal' },
  { value: 'veo', label: 'Google Veo 3' },
];

export function parseFamixaMotionEngine(raw?: string | null): FamixaMotionEngine {
  const v = (raw || '').trim().toLowerCase();
  if (v === 'wan') return 'wan';
  if (v === 'veo') return 'veo';
  return 'turbo';
}

export function loadFamixaMotionEngine(): FamixaMotionEngine {
  try {
    return parseFamixaMotionEngine(localStorage.getItem(FAMIXA_MOTION_ENGINE_KEY));
  } catch {
    return 'turbo';
  }
}

export function persistFamixaMotionEngine(next: FamixaMotionEngine) {
  try {
    localStorage.setItem(FAMIXA_MOTION_ENGINE_KEY, next);
  } catch {
    /* quota */
  }
}

export function motionProviderFromEngine(engine: FamixaMotionEngine): 'runway' | 'wan' | 'google' {
  if (engine === 'wan') return 'wan';
  if (engine === 'veo') return 'google';
  return 'runway';
}

export function famixaMotionEngineReady(
  engine: FamixaMotionEngine,
  keys: { runway?: boolean; fal?: boolean; veo?: boolean },
) {
  if (engine === 'wan') return Boolean(keys.fal);
  if (engine === 'veo') return Boolean(keys.veo);
  return Boolean(keys.runway);
}
