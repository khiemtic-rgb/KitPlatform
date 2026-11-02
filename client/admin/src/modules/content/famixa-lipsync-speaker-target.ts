/** FAMIXA LipSync speaker WHAT. Character id only — no Fal fields. */

export const LIPSYNC_SPEAKER_TARGET_ID = 'FAMIXA_LIPSYNC_SPEAKER_TARGET_V1';

export type LipSyncSpeakerFaceMark = {
  x: number;
  y: number;
  width: number;
  height: number;
  characterId?: string;
  /** Deterministic marking frame. Not video.currentTime. */
  frameNumber?: number;
};

export type LipSyncSpeakerCharacter = { id?: string; name?: string };

function trimOf(v?: string) {
  return (v || '').trim();
}

function normName(v?: string) {
  return trimOf(v).toLowerCase();
}

function normId(v?: string) {
  return trimOf(v).toUpperCase();
}

/** Shot-local face side. Linh is right on SH01; Minh is left. Resolve via character name/id. */
const SIDE_ALIASES: Record<'left' | 'right', string[]> = {
  right: ['linh', 'char-003'],
  left: ['minh', 'char-001'],
};

export function lipsyncSpeakerSideOfFaceMark(mark?: { x?: number }): 'left' | 'right' | undefined {
  if (!mark || !Number.isFinite(Number(mark.x))) return undefined;
  return Number(mark.x) > 0.5 ? 'right' : 'left';
}

export function lipsyncSpeakerCharacterIdFromSide(
  side: 'left' | 'right',
  characters?: LipSyncSpeakerCharacter[],
): string | undefined {
  const aliases = SIDE_ALIASES[side];
  for (const row of characters ?? []) {
    const id = trimOf(row.id);
    const name = normName(row.name);
    const nid = normId(row.id);
    if (aliases.includes(name) || aliases.includes(nid.toLowerCase())) return id || undefined;
  }
  return undefined;
}

export function lipsyncSpeakerCharacterIdFromFaceMark(
  mark: { x?: number; characterId?: string } | undefined,
  characters?: LipSyncSpeakerCharacter[],
): string | undefined {
  const stamped = trimOf(mark?.characterId);
  if (stamped) return stamped;
  const side = lipsyncSpeakerSideOfFaceMark(mark);
  if (!side) return undefined;
  return lipsyncSpeakerCharacterIdFromSide(side, characters);
}

export const LIPSYNC_SPEAKER_MARK_FRAME_NUMBER = 0;

/** 720p two-shot: y above this is torso, not a face. Fal then auto-moves every mouth. */
export const LIPSYNC_SPEAKER_FACE_Y_MAX = 0.34;

export function lipsyncSpeakerPickOf(
  side: 'left' | 'right',
  characters?: LipSyncSpeakerCharacter[],
  size?: { width: number; height: number },
): LipSyncSpeakerFaceMark {
  return {
    x: side === 'right' ? 0.63 : 0.3,
    y: side === 'right' ? 0.25 : 0.48,
    width: size?.width && size.width > 0 ? size.width : 1280,
    height: size?.height && size.height > 0 ? size.height : 720,
    characterId: lipsyncSpeakerCharacterIdFromSide(side, characters),
    frameNumber: LIPSYNC_SPEAKER_MARK_FRAME_NUMBER,
  };
}

export function isSpeakerFaceBandMark(mark?: { x?: number; y?: number }) {
  const y = Number(mark?.y);
  return Number.isFinite(y) && y >= 0.12 && y <= LIPSYNC_SPEAKER_FACE_Y_MAX;
}

export function stampLipsyncSpeakerPick(
  mark: { x: number; y: number; width: number; height: number; characterId?: string; frameNumber?: number },
  characters?: LipSyncSpeakerCharacter[],
): LipSyncSpeakerFaceMark {
  return {
    x: mark.x,
    y: mark.y,
    width: mark.width,
    height: mark.height,
    characterId: lipsyncSpeakerCharacterIdFromFaceMark(mark, characters),
    frameNumber:
      Number.isInteger(mark.frameNumber) && Number(mark.frameNumber) >= 0
        ? Number(mark.frameNumber)
        : LIPSYNC_SPEAKER_MARK_FRAME_NUMBER,
  };
}

export function validSpeakerFaceRegion(region?: { x?: number; y?: number; width?: number; height?: number }) {
  if (!region) return false;
  const x = region.x;
  const y = region.y;
  if (x == null || y == null) return false;
  if (!(x >= 0 && x <= 1 && y >= 0 && y <= 1)) return false;
  if (!(Number(region.width) > 0 && Number(region.height) > 0)) return false;
  return true;
}

/** Multi-face shots fail closed. Fal must not pick the prominent face. */
export function assertLipSyncSpeakerTargetReady(opts: {
  multiFace: boolean;
  speakerCharacterId?: string;
  speakerFaceRegion?: { x?: number; y?: number; width?: number; height?: number };
  requiredCharacterId?: string;
}) {
  if (!opts.multiFace) return;
  const id = trimOf(opts.speakerCharacterId);
  if (!id) throw new Error('LipSync speakerCharacterId missing — không gửi Fal.');
  if (opts.requiredCharacterId && id !== trimOf(opts.requiredCharacterId)) {
    throw new Error('LipSync speakerCharacterId mismatch — không gửi Fal.');
  }
  if (!validSpeakerFaceRegion(opts.speakerFaceRegion)) {
    throw new Error('LipSync speakerFaceRegion missing — không gửi Fal.');
  }
}

export function frozenSpeakerCharacterIdOf(frozen?: { speakerCharacterId?: string } | null) {
  return trimOf(frozen?.speakerCharacterId) || undefined;
}

export function assertFrozenSpeakerTargetUnchanged(
  frozen: { speakerCharacterId?: string },
  next?: string,
) {
  const locked = frozenSpeakerCharacterIdOf(frozen);
  const incoming = trimOf(next);
  if (locked && incoming && incoming !== locked) {
    throw new Error('Frozen speakerCharacterId is immutable.');
  }
}

/** Fal accepted ASD schema but could not lock a face at the pin. Next remake may omit pin or retarget. */
export function lipsyncSpeakerPinRejectedByFal(error?: string) {
  const t = (error || '').toLowerCase();
  return (
    t.includes('selected speaker face') ||
    t.includes('invalid active speaker detection') ||
    t.includes('select the face again') ||
    t.includes('try auto')
  );
}

/** Two-face shots must keep a face pin. Auto moves the silent character. */
export function shouldOmitRejectedSpeakerPin(opts: {
  remake?: boolean;
  error?: string;
  frozenRegion?: { x?: number; y?: number };
  liveMark?: { x?: number; y?: number };
}) {
  if (isSpeakerFaceBandMark(opts.liveMark)) return false;
  return Boolean(opts.remake && lipsyncSpeakerPinRejectedByFal(opts.error) && !isSpeakerFaceBandMark(opts.frozenRegion));
}

/** After freeze, UI must not change the provider target — unless Fal rejected that pin. */
export function canMutateLiveSpeakerTarget(
  frozen?: { speakerCharacterId?: string } | null,
  run?: { lipsyncError?: string },
) {
  if (lipsyncSpeakerPinRejectedByFal(run?.lipsyncError)) return true;
  return !frozenSpeakerCharacterIdOf(frozen);
}
