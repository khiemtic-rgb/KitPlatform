/** Famixa is a Project consumer — mapping lives here, not inside the engine. */

import type { KitVideoProductionState } from './kit-video-engine';

export const FAMIXA_PROJECT = 'FAMIXA';
export const FAMIXA_UNIVERSE = 'FAMILY_A';

export type StudioProjectContext = {
  projectCode: string;
  brandId?: string | null;
  filmTypeId?: string | null;
  universes?: { universeCode: string }[];
};

/** Constants stay until the project row itself has both brand and film type. */
export function resolveStudioProject(project: StudioProjectContext | null | undefined): {
  projectCode: string;
  universeCode: string;
} {
  if (project?.brandId && project.filmTypeId) {
    const universe =
      project.universes?.find((row) => row.universeCode === FAMIXA_UNIVERSE)?.universeCode
      ?? project.universes?.[0]?.universeCode
      ?? FAMIXA_UNIVERSE;
    return { projectCode: project.projectCode, universeCode: universe };
  }
  return { projectCode: FAMIXA_PROJECT, universeCode: FAMIXA_UNIVERSE };
}

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
