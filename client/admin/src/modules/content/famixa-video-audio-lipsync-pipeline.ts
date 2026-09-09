/** FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1 — contracts, stages, preflight, finalize. No providers. */

import { linesForShot } from './content-famixa-dialogue-map';
import { resolveTakeUrl } from './content-famixa-final-source';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';
import { VISUAL_MODE_3D } from './kit-video-visual-mode';

export const DOCUMENT_ID = 'FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1';
export const SUITE_ID = 'FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1_REGRESSION';

export const PROJECT_VISUAL_MODE = VISUAL_MODE_3D;
export const I2V_PROVIDER = 'runway';
export const I2V_MODEL = 'gen4_turbo';
export const I2V_IS_MUTE_TAKE = true;
export const LIPSYNC_PROVIDER = 'fal';
export const LIPSYNC_ENDPOINT = 'fal-ai/sync-lipsync';
export const TTS_PROVIDER = 'elevenlabs';
export const TTS_MODEL = 'eleven_v3';
export const CANONICAL_FINAL_RENDER = 'SERVER_FFMPEG';
export const BROWSER_WEBM_ROLE = 'PREVIEW_ONLY';
export const MIX_STAFF_EPISODE_ROLE = 'PREVIEW_ONLY';
export const STAFF_OVERLAY_ELIGIBLE_FOR_FINAL = false;
export const MULTI_SPEAKER_LIPSYNC_SUPPORTED = false;
export const LISTENER_MOUTH_CONTROL = false;
export const LAST_FRAME_USED = false;
export const INVENT_VOICE_ID = false;
export const INVENT_SECOND_VISUAL_COMPILER = false;

export const SPEECH_LEAD_IN = 0.2;
export const SPEECH_TAIL = 0.22;
export const I2V_ALLOWED_SEC = [5, 10] as const;
export const DIALOGUE_LINEAR_GAIN = 1;
export const CANONICAL_SAMPLE_RATE = 48000;
export const CANONICAL_CHANNELS = 2;
export const CANONICAL_AUDIO_CODEC = 'aac';

export const GATE = {
  LIPSYNC_REQUIRED: 'LIPSYNC_REQUIRED',
  VOICE_REQUIRED: 'VOICE_REQUIRED',
  MULTI_SPEAKER_LIPSYNC_UNSUPPORTED: 'MULTI_SPEAKER_LIPSYNC_UNSUPPORTED',
  VISUAL_MODE_CONFLICT: 'VISUAL_MODE_CONFLICT',
  LEGACY_PHOTOREAL: 'LEGACY_PHOTOREAL_CANON',
  MISSING_KF: 'MISSING_CANONICAL_KEYFRAME',
  MISSING_TAKE: 'MISSING_MUTE_TAKE',
  DURATION_OVERFLOW: 'DIALOGUE_EXCEEDS_I2V_CAP',
  MISSING_SPEAKER: 'SPEAKER_REQUIRED',
  NOT_ELIGIBLE_FOR_FINAL: 'NOT_ELIGIBLE_FOR_FINAL',
  PREVIEW_ONLY: 'PREVIEW_ONLY',
} as const;

export type ShotKind = 'SILENT_SHOT' | 'SINGLE_SPEAKER_SHOT' | 'MULTI_SPEAKER_SHOT' | 'AMBIENCE_ONLY_SHOT';

export type ProductionStage =
  | 'DRAFT'
  | 'VISUAL_READY'
  | 'VOICE_READY'
  | 'VIDEO_READY'
  | 'LIPSYNC_READY'
  | 'MIX_READY'
  | 'FINAL_READY'
  | 'DIRECTOR_REVIEW'
  | 'APPROVED'
  | 'BLOCKED'
  | 'FAILED'
  | 'REVIEW_REQUIRED';

export type AvSyncStatus = 'PASS' | 'WARNING' | 'FAIL';
export type VoiceProfileStatus = 'UNASSIGNED' | 'ASSIGNED';
export type ArtifactRole = 'CANONICAL' | 'PREVIEW_ONLY' | 'DEPRECATED';

export type DialogueCue = {
  dialogueId: string;
  shotId: string;
  speakerCharacterId: string;
  text: string;
  voiceId?: string;
  startSec: number;
  endSec: number;
  audioAssetId?: string;
  audioDurationSec?: number;
  status: 'MISSING' | 'READY';
};

export type CharacterVoiceProfile = {
  characterId: string;
  voiceId?: string;
  status: VoiceProfileStatus;
  canonical: false;
};

export type VideoTake = {
  takeId: string;
  shotId: string;
  provider: typeof I2V_PROVIDER;
  model: typeof I2V_MODEL;
  durationSec: number;
  muted: true;
  visualMode: string;
  status: 'READY' | 'MISSING';
};

export type LipSyncArtifact = {
  lipSyncArtifactId: string;
  shotId: string;
  videoTakeId?: string;
  audioAssetId?: string;
  provider: typeof LIPSYNC_PROVIDER;
  status: 'PENDING' | 'READY' | 'FAILED' | 'NOT_REQUIRED' | 'UNSUPPORTED';
};

export type MixIntent = {
  room: boolean;
  music: boolean;
  loudnorm: boolean;
  sfxCount: number;
};

export type ShotProductionInput = {
  shotId: string;
  hasCanonicalKf: boolean;
  kfVisualMode?: string;
  projectVisualMode?: string;
  identityLocked?: boolean;
  legacyPhotorealAttached?: boolean;
  cues: DialogueCue[];
  hasMuteTake: boolean;
  takeDurationSec?: number;
  hasLipSync: boolean;
  mixReady?: boolean;
  directorApproved?: boolean;
  clientMixOnly?: boolean;
  overlayPreview?: boolean;
};

export type PreflightResult = {
  allowed: boolean;
  blockers: string[];
  warnings: string[];
  stage: ProductionStage;
  kind: ShotKind;
  finalSource: 'LIPSYNC_VIDEO' | 'I2V_ALLOWED' | 'RAW_I2V' | 'TTS_OVERLAY' | 'NONE';
  voiceReady: boolean;
  videoReady: boolean;
  lipSyncReady: boolean;
  mixReady: boolean;
  finalReady: boolean;
  avSync: AvSyncStatus;
  videoDurationSec: number;
  audioDurationSec: number;
  finalDurationSec: number;
};

export function characterVoiceProfile(characterId: string, voiceId?: string): CharacterVoiceProfile {
  const id = (voiceId ?? '').trim();
  return { characterId, voiceId: id || undefined, status: id ? 'ASSIGNED' : 'UNASSIGNED', canonical: false };
}

export function uniqueSpeakers(cues: Pick<DialogueCue, 'speakerCharacterId'>[]) {
  return [...new Set(cues.map((c) => (c.speakerCharacterId || '').trim()).filter(Boolean))];
}

export function classifyShot(cues: DialogueCue[]): ShotKind {
  const n = uniqueSpeakers(cues).length;
  if (n === 0) return 'SILENT_SHOT';
  if (n === 1) return 'SINGLE_SPEAKER_SHOT';
  return 'MULTI_SPEAKER_SHOT';
}

export function cueHasSpeaker(cue: Pick<DialogueCue, 'speakerCharacterId'>) {
  return Boolean((cue.speakerCharacterId || '').trim());
}

export function mergeSameSpeakerCues(cues: DialogueCue[]): { cues: DialogueCue[]; discarded: string[]; mergedIds: string[] } {
  if (cues.length <= 1) return { cues: [...cues], discarded: [], mergedIds: cues.map((c) => c.dialogueId) };
  const speakers = uniqueSpeakers(cues);
  if (speakers.length !== 1) return { cues: [...cues], discarded: [], mergedIds: cues.map((c) => c.dialogueId) };
  const first = cues[0]!;
  const last = cues[cues.length - 1]!;
  const duration = cues.reduce((n, c) => n + (c.audioDurationSec || Math.max(0, c.endSec - c.startSec)), 0);
  return {
    discarded: [],
    mergedIds: cues.map((c) => c.dialogueId),
    cues: [
      {
        ...first,
        dialogueId: first.dialogueId,
        text: cues.map((c) => c.text).join(' '),
        startSec: first.startSec,
        endSec: last.endSec,
        audioDurationSec: duration,
        status: cues.every((c) => c.status === 'READY') ? 'READY' : 'MISSING',
      },
    ],
  };
}

export function i2vDurationForDialogue(lastAudioEndSec: number): 5 | 10 {
  const need = Math.max(0, lastAudioEndSec) + SPEECH_TAIL;
  return need <= 5.5 ? 5 : 10;
}

export function dialogueExceedsI2vCap(lastAudioEndSec: number) {
  return lastAudioEndSec + SPEECH_TAIL > 10 + 0.05;
}

export function stitchSeconds(opts: { kind: ShotKind; takeDurationSec?: number; lastAudioEndSec?: number }) {
  if (opts.kind === 'SILENT_SHOT' || opts.kind === 'AMBIENCE_ONLY_SHOT') {
    return opts.takeDurationSec && opts.takeDurationSec > 0 ? opts.takeDurationSec : 5;
  }
  const speech = (opts.lastAudioEndSec ?? 0) + SPEECH_TAIL;
  const take = opts.takeDurationSec && opts.takeDurationSec > 0 ? opts.takeDurationSec : i2vDurationForDialogue(opts.lastAudioEndSec ?? 0);
  return Math.max(speech, take);
}

export function avSyncStatus(videoSec: number, audioSec: number): AvSyncStatus {
  const v = Number(videoSec) || 0;
  const a = Number(audioSec) || 0;
  if (v <= 0 && a <= 0) return 'FAIL';
  const delta = Math.abs(v - a);
  if (delta <= 0.12) return 'PASS';
  if (delta <= 0.4) return 'WARNING';
  return 'FAIL';
}

export function voiceReadyOf(cues: DialogueCue[]) {
  if (!cues.length) return true;
  return cues.every(
    (c) =>
      cueHasSpeaker(c) &&
      Boolean((c.voiceId || '').trim()) &&
      Boolean((c.audioAssetId || '').trim()) &&
      (c.audioDurationSec || 0) > 0.2 &&
      c.status === 'READY',
  );
}

/** Session/IDB blob or object URL. Duration alone is not an artifact. */
export function hasResolvableVoiceArtifact(opts: {
  durationSec: number;
  voiceId?: string;
  ttsFile?: { url?: string; fileName?: string };
  hasBlob?: boolean;
}) {
  const dur = Number(opts.durationSec) || 0;
  if (dur <= 0.2 || !(opts.voiceId || '').trim()) return false;
  const url = (opts.ttsFile?.url || '').trim();
  const name = (opts.ttsFile?.fileName || '').trim();
  return Boolean(url || name || opts.hasBlob);
}

export function retryTouches(stage: 'lipsync' | 'mix' | 'i2v' | 'voice') {
  return {
    keyframe: false,
    i2v: stage === 'i2v',
    voice: stage === 'voice',
    lipsync: stage === 'lipsync',
    mix: stage === 'mix',
    character: false,
    visualMode: false,
    pvs: false,
    vua: false,
    cdl: false,
  };
}

export function videoProductionPreflight(input: ShotProductionInput): PreflightResult {
  const blockers: string[] = [];
  const warnings: string[] = [];
  const projectMode = input.projectVisualMode || PROJECT_VISUAL_MODE;
  const kind = classifyShot(input.cues);
  const spoken = kind === 'SINGLE_SPEAKER_SHOT' || kind === 'MULTI_SPEAKER_SHOT';
  const lastEnd = input.cues.reduce((n, c) => Math.max(n, c.endSec, c.startSec + (c.audioDurationSec || 0)), 0);
  const audioDurationSec = spoken ? lastEnd : 0;
  const videoDurationSec = input.takeDurationSec || (spoken ? i2vDurationForDialogue(lastEnd) : 5);
  const finalDurationSec = stitchSeconds({ kind, takeDurationSec: input.takeDurationSec, lastAudioEndSec: lastEnd });

  if (input.legacyPhotorealAttached) blockers.push(GATE.LEGACY_PHOTOREAL);
  if (input.kfVisualMode && input.kfVisualMode !== projectMode) blockers.push(GATE.VISUAL_MODE_CONFLICT);
  if (input.cues.some((c) => !cueHasSpeaker(c))) blockers.push(GATE.MISSING_SPEAKER);
  if (!input.hasCanonicalKf) blockers.push(GATE.MISSING_KF);

  const voiceReady = voiceReadyOf(input.cues);
  if (spoken && !voiceReady) blockers.push(GATE.VOICE_REQUIRED);
  if (spoken && dialogueExceedsI2vCap(lastEnd)) warnings.push(GATE.DURATION_OVERFLOW);

  const videoReady = input.hasCanonicalKf && input.hasMuteTake;
  if (!videoReady && input.hasCanonicalKf) blockers.push(GATE.MISSING_TAKE);

  let lipSyncReady = !spoken;
  let finalSource: PreflightResult['finalSource'] = 'NONE';

  if (!spoken) {
    lipSyncReady = true;
    finalSource = videoReady ? 'I2V_ALLOWED' : 'NONE';
  } else if (kind === 'MULTI_SPEAKER_SHOT') {
    lipSyncReady = false;
    blockers.push(GATE.MULTI_SPEAKER_LIPSYNC_UNSUPPORTED);
    finalSource = 'NONE';
  } else if (!input.hasLipSync) {
    lipSyncReady = false;
    blockers.push(GATE.LIPSYNC_REQUIRED);
    finalSource = input.overlayPreview ? 'TTS_OVERLAY' : 'RAW_I2V';
  } else {
    lipSyncReady = true;
    finalSource = 'LIPSYNC_VIDEO';
  }

  if (input.overlayPreview && spoken) {
    blockers.push(GATE.PREVIEW_ONLY);
    blockers.push(GATE.NOT_ELIGIBLE_FOR_FINAL);
  }
  if (input.clientMixOnly) warnings.push('CLIENT_MIX_NOT_CANONICAL');

  const mixReady = spoken ? Boolean(input.mixReady) && kind === 'SINGLE_SPEAKER_SHOT' : videoReady && input.mixReady !== false;
  const finalReady =
    !blockers.includes(GATE.LEGACY_PHOTOREAL) &&
    !blockers.includes(GATE.VISUAL_MODE_CONFLICT) &&
    !blockers.includes(GATE.MISSING_SPEAKER) &&
    input.hasCanonicalKf &&
    videoReady &&
    (!spoken || (voiceReady && lipSyncReady && mixReady && finalSource === 'LIPSYNC_VIDEO')) &&
    (spoken || finalSource === 'I2V_ALLOWED') &&
    !input.overlayPreview &&
    kind !== 'MULTI_SPEAKER_SHOT';

  let stage: ProductionStage = 'DRAFT';
  if (input.hasCanonicalKf) stage = 'VISUAL_READY';
  if (input.hasCanonicalKf && (!spoken || voiceReady)) stage = 'VOICE_READY';
  if (videoReady) stage = 'VIDEO_READY';
  if (videoReady && lipSyncReady) stage = 'LIPSYNC_READY';
  if (videoReady && lipSyncReady && mixReady) stage = 'MIX_READY';
  if (finalReady) stage = input.directorApproved ? 'APPROVED' : 'DIRECTOR_REVIEW';
  if (blockers.length && stage === 'VIDEO_READY' && spoken) stage = 'BLOCKED';
  if (finalReady && !input.directorApproved) stage = 'DIRECTOR_REVIEW';
  if (finalReady && input.directorApproved) stage = 'APPROVED';

  return {
    allowed: finalReady,
    blockers: [...new Set(blockers)],
    warnings: [...new Set(warnings)],
    stage,
    kind,
    finalSource,
    voiceReady,
    videoReady,
    lipSyncReady,
    mixReady: mixReady || (!spoken && videoReady),
    finalReady,
    avSync: avSyncStatus(videoDurationSec, spoken ? Math.max(audioDurationSec, videoDurationSec) : videoDurationSec),
    videoDurationSec,
    audioDurationSec,
    finalDurationSec,
  };
}

export function shotProductionInputOf(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  ttsFiles: Record<string, { url: string; fileName: string }>,
  hasVoiceFile?: (lineId: string) => boolean,
): ShotProductionInput {
  const run = shotRunOf(state, shot);
  const lines = linesForShot(state, shot);
  let t = 0.2;
  const cues: DialogueCue[] = lines.map((line) => {
    const dur = Number(state.voiceAssets?.[line.id]?.duration || 0);
    const ready = hasResolvableVoiceArtifact({
      durationSec: dur,
      voiceId: line.voiceId,
      ttsFile: ttsFiles[line.id],
      hasBlob: hasVoiceFile?.(line.id),
    });
    const cue: DialogueCue = {
      dialogueId: line.id,
      shotId: shot.id,
      speakerCharacterId: line.characterId || '',
      text: line.text,
      voiceId: line.voiceId,
      startSec: t,
      endSec: t + (dur || 1),
      audioAssetId: ready ? line.id : undefined,
      audioDurationSec: dur,
      status: ready ? 'READY' : 'MISSING',
    };
    t += (dur || 1) + 0.1;
    return cue;
  });
  const spoken = cues.length > 0;
  const hasLip = Boolean(run.lipsynced || run.lipsyncUrl?.trim() || run.finalSource === 'FAL');
  return {
    shotId: shot.id,
    hasCanonicalKf: Boolean(
      (run.kfApproved || run.status === 'approved') &&
        (run.keyframeDataUrl?.startsWith('data:image') || Boolean((run.kfSourceHash || '').trim())),
    ),
    kfVisualMode: PROJECT_VISUAL_MODE,
    projectVisualMode: PROJECT_VISUAL_MODE,
    identityLocked: true,
    legacyPhotorealAttached: false,
    cues,
    hasMuteTake: Boolean(resolveTakeUrl(run)),
    takeDurationSec: shot.seconds || (spoken ? i2vDurationForDialogue(t) : 5),
    hasLipSync: hasLip,
    mixReady: spoken ? hasLip : true,
    overlayPreview: false,
  };
}

export function canFinalizeShot(input: ShotProductionInput) {
  return videoProductionPreflight(input);
}

export function episodeCanFinalize(shots: ShotProductionInput[]) {
  if (!shots.length) return { allowed: false, blockers: ['NO_SHOTS'], ready: 0, total: 0 };
  const rows = shots.map((s) => videoProductionPreflight(s));
  const blockers = rows.flatMap((r, i) => r.blockers.map((b) => `${shots[i]?.shotId}:${b}`));
  return { allowed: rows.every((r) => r.finalReady), blockers, ready: rows.filter((r) => r.finalReady).length, total: rows.length };
}

export function staffStageCopy(row: PreflightResult) {
  const visual = row.stage === 'DRAFT' ? { mark: '○', label: 'Chưa có keyframe' } : { mark: '✓', label: 'Keyframe Ready' };
  const voice = !['SINGLE_SPEAKER_SHOT', 'MULTI_SPEAKER_SHOT'].includes(row.kind)
    ? { mark: '—', label: 'Không cần thoại' }
    : row.voiceReady
      ? { mark: '✓', label: 'Voice Ready' }
      : { mark: '⚠', label: 'Cần Voice' };
  const video = row.videoReady
    ? { mark: '✓', label: 'Hình ảnh đã tạo (take câm)' }
    : { mark: '○', label: 'Chưa có I2V' };
  const lip = row.kind === 'SILENT_SHOT' || row.kind === 'AMBIENCE_ONLY_SHOT'
    ? { mark: '—', label: 'Không cần lip-sync' }
    : row.kind === 'MULTI_SPEAKER_SHOT'
      ? { mark: '⚠', label: 'NOT SUPPORTED FOR MULTI-SPEAKER' }
      : row.lipSyncReady
        ? { mark: '✓', label: 'Lip-sync Ready' }
        : { mark: '⚠', label: 'REQUIRED' };
  const mix = row.mixReady ? { mark: '✓', label: 'Mix Ready' } : { mark: '○', label: 'Chờ mix server' };
  const fin = row.finalReady
    ? { mark: '✓', label: 'Ready for Director Review' }
    : { mark: '○', label: 'BLOCKED' };
  return { visual, voice, video, lip, mix, fin };
}

export function falseFinalBlocked(input: ShotProductionInput) {
  const row = videoProductionPreflight({ ...input, overlayPreview: true });
  return row.blockers.includes(GATE.NOT_ELIGIBLE_FOR_FINAL) && row.blockers.includes(GATE.LIPSYNC_REQUIRED);
}
