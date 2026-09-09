/** Famixa is a Project consumer — mapping lives here, not inside the engine. */

import type { KitVideoProductionState } from './kit-video-engine';

export const FAMIXA_PROJECT = 'FAMIXA';
export const FAMIXA_UNIVERSE = 'FAMILY_A';

/** series_build.status → engine production state. Adapter only. */
export function mapFamixaBuildToEngine(status: string): KitVideoProductionState {
  switch ((status || '').toLowerCase()) {
    case 'script_locked':
      return 'SCRIPT_APPROVED';
    case 'voice_locked':
      return 'SHOT_PLANNED';
    case 'in_prod':
      return 'KEYFRAME_GENERATION';
    case 'final':
      return 'FINAL';
    default:
      return 'DRAFT';
  }
}
