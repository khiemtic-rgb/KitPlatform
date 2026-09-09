/** Production orchestration intent only. Not a visual or character authority. */

import { linesForShot, multiSpeakerBlock } from '../content-famixa-dialogue-map';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState } from '../content-famixa-series';
import { characterVoiceProfile } from '../famixa-video-audio-lipsync-pipeline';

export const SHOT_PRODUCTION_ORCHESTRATION_ID = 'FAMIXA_SHOT_PRODUCTION_ORCHESTRATION_V1';

export type ShotProductionMode = 'SILENT' | 'SPOKEN' | 'MULTI_SPEAKER_BLOCKED';
export type DurationIntent = 'AUTO_FROM_VOICE' | 'FIXED';
export type ContinuityReference = 'PREVIOUS_SHOT' | 'NONE';

export type ShotProductionIntent = {
  shotId: string;
  sceneId?: string;
  characterIds: string[];
  action: string;
  emotion?: string;
  acting?: string;
  blocking?: string;
  camera?: string;
  dialogueSegmentIds: string[];
  durationIntent: DurationIntent;
  continuityReference: ContinuityReference;
  productionMode: ShotProductionMode;
};

export function shotProductionOrchestrationEnabled() {
  if (typeof localStorage === 'undefined') return true;
  return localStorage.getItem('famixa-shot-orch') !== '0';
}

export function shotProductionIntentOf(state: SeriesPilotState, shot: FamixaSeriesShot): ShotProductionIntent {
  const lines = linesForShot(state, shot);
  const ids = Array.isArray(shot.dialogueSegmentIds)
    ? shot.dialogueSegmentIds
    : lines.map((l) => l.id);
  const speakers = [...new Set(lines.map((l) => (l.characterId || '').trim()).filter(Boolean))];
  const multi = Boolean(multiSpeakerBlock(lines));
  const spoken = ids.length > 0 && lines.length > 0;
  const prev = previousApprovedShot(state, shot);
  const first = lines[0];
  return {
    shotId: shot.id,
    sceneId: shot.sceneId || shot.scene,
    characterIds: shot.characterIds?.length ? shot.characterIds : speakers,
    action: (shot.shotAction || shot.story || '').trim() || shot.visual || '',
    emotion: first?.performance?.emotion,
    acting: first?.performance?.label || (first?.performance?.intensity != null ? String(first.performance.intensity) : undefined),
    blocking: shot.visual,
    camera: shot.motionPromptVi || shot.motionPrompt,
    dialogueSegmentIds: ids,
    durationIntent: spoken ? 'AUTO_FROM_VOICE' : 'FIXED',
    continuityReference: prev ? 'PREVIOUS_SHOT' : 'NONE',
    productionMode: multi ? 'MULTI_SPEAKER_BLOCKED' : spoken ? 'SPOKEN' : 'SILENT',
  };
}

export function resolveAssignedVoiceId(state: SeriesPilotState, characterId: string) {
  const ch = (state.characters ?? []).find((c) => c.id === characterId);
  return characterVoiceProfile(characterId, ch?.voiceId);
}

function previousApprovedShot(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const shots = state.episode?.shots ?? [];
  const idx = shots.findIndex((s) => s.id === shot.id);
  for (let i = idx - 1; i >= 0; i--) {
    const prev = shots[i]!;
    const run = shotRunOf(state, prev);
    if (run.keyframeDataUrl && (run.kfApproved || run.status === 'approved')) return prev;
  }
  return undefined;
}
