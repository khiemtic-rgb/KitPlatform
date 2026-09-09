/** Director Acting Beat UI — writes existing shot.actingBeat only. Not a new domain. */

import { ACTING_EMOTIONS, type ActingEmotion } from '../content-famixa-acting-law';
import type { FamixaSeriesShot } from '../content-famixa-series';
import { SPEECH_LEAD_IN, SPEECH_TAIL, type ShotActingBeat } from '../famixa-shot-production-timing';

/** Same VI stems as content-famixa-acting-law labelOf. English keys stay on actingBeat. */
export const ACTING_EMOTION_VI: Record<ActingEmotion, string> = {
  neutral: 'bình thường',
  uneasy: 'mong manh',
  tense: 'căng',
  annoyed: 'bực',
  burst: 'bùng',
  silent: 'im',
  hurt: 'đau',
  soft: 'dịu',
  aftertaste: 'dư âm',
};

export const ACTING_PAPER_PROP_EN = 'He holds a sheet of paper with both hands.';
export const ACTING_PAPER_PROP_VI = 'Cầm tờ giấy bằng hai tay';
export const ACTING_PHONE_PROP_EN = 'Holds and looks at a phone.';
export const ACTING_PHONE_PROP_VI = 'Cầm / nhìn điện thoại';

export const ACTING_STANCE_DOORWAY = 'Standing at the living-room doorway.';
export const ACTING_STANCE_SIT = 'Sitting on the sofa.';
export const ACTING_BODY_WEIGHT = 'He shifts his weight slightly and hesitates.';
export const ACTING_BODY_SEATED = 'Remains seated and makes only a small natural movement.';
export const ACTING_GAZE_PHONE = 'Keeps looking at the phone and does not look at the camera.';
export const ACTING_GAZE_NO_LOOKUP = 'Does not look up; keeps looking at the phone.';
export const ACTING_ROOM_STILL = 'The living room behind him stays still; only he moves.';
export const ACTING_ROOM_SIT_STILL = 'The living room remains still; only the speaker makes subtle movement.';

export const INVALID_CONTEXTUAL_BEAT = 'INVALID_CONTEXTUAL_BEAT';

export const ACTING_VISUAL_SIT = /ngồi|sofa|ghế|seated|sitting/i;
export const ACTING_VISUAL_DOOR = /cửa phòng|đứng ở cửa|doorway/i;
export const ACTING_VISUAL_STAND = /đứng|standing|doorway|cửa phòng/i;
export const ACTING_VISUAL_MOM = /nhìn mẹ|toward (?:his )?mother/i;
export const ACTING_VISUAL_PHONE = /điện thoại|phone/i;
export const ACTING_VISUAL_PAPER = /tờ giấy|cầm giấy|cầm tờ|sheet of paper/i;
export const ACTING_VISUAL_NO_LOOKUP = /không ngẩng|does not look up|doesn't look up/i;

export type ActingShotStance = 'sitting' | 'doorway' | 'standing' | 'unknown';

export type ActingChipApplicability = {
  stance?: ActingShotStance[];
  visualContext?: RegExp;
};

export type ActingCatalogChip = {
  id: string;
  label: string;
  value: string;
  applicability?: ActingChipApplicability;
};

/** Stance only. Do not put body verbs here. Doorway chips are contextual, not universal. */
export const ACTING_ACTION_CHIPS: readonly ActingCatalogChip[] = [
  { id: 'doorway', label: 'Đứng ở cửa', value: ACTING_STANCE_DOORWAY, applicability: { stance: ['doorway'], visualContext: ACTING_VISUAL_DOOR } },
  { id: 'hesitate', label: 'Hơi do dự', value: ACTING_STANCE_DOORWAY, applicability: { stance: ['doorway'], visualContext: ACTING_VISUAL_DOOR } },
  { id: 'sit', label: 'Ngồi yên trên ghế', value: ACTING_STANCE_SIT, applicability: { stance: ['sitting'], visualContext: ACTING_VISUAL_SIT } },
];

export const ACTING_BODY_CHIPS: readonly ActingCatalogChip[] = [
  { id: 'still', label: 'Không di chuyển', value: 'He stays in place.' },
  { id: 'weight', label: 'Hơi chuyển trọng tâm', value: ACTING_BODY_WEIGHT, applicability: { stance: ['doorway', 'standing'], visualContext: ACTING_VISUAL_STAND } },
  { id: 'posture', label: 'Hơi điều chỉnh tư thế', value: 'He adjusts his posture slightly.' },
  { id: 'paper', label: 'Khẽ động tờ giấy', value: 'The paper shifts slightly in his hands.', applicability: { visualContext: ACTING_VISUAL_PAPER } },
  { id: 'step', label: 'Bước nhẹ một bước', value: 'He takes a small half-step.', applicability: { stance: ['doorway', 'standing'], visualContext: ACTING_VISUAL_STAND } },
  { id: 'seated', label: 'Chỉ chuyển động nhẹ', value: ACTING_BODY_SEATED, applicability: { stance: ['sitting'], visualContext: ACTING_VISUAL_SIT } },
];

export const ACTING_GAZE_CHIPS: readonly ActingCatalogChip[] = [
  { id: 'mom', label: 'Nhìn mẹ', value: 'He looks toward his mother, not the camera.', applicability: { visualContext: ACTING_VISUAL_MOM } },
  { id: 'lower', label: 'Hạ mắt rồi nhìn mẹ', value: 'Looks toward his mother, then lowers his gaze slightly; not at the camera.', applicability: { visualContext: ACTING_VISUAL_MOM } },
  { id: 'nocam', label: 'Không nhìn camera', value: 'not at the camera' },
  { id: 'phone', label: 'Nhìn điện thoại', value: ACTING_GAZE_PHONE, applicability: { visualContext: ACTING_VISUAL_PHONE } },
  { id: 'noup', label: 'Không ngẩng lên', value: ACTING_GAZE_NO_LOOKUP, applicability: { visualContext: ACTING_VISUAL_NO_LOOKUP } },
  { id: 'phonehold', label: 'Giữ ánh mắt trên điện thoại', value: ACTING_GAZE_PHONE, applicability: { visualContext: ACTING_VISUAL_PHONE } },
];

export const ACTING_ROOM_CHIPS: readonly ActingCatalogChip[] = [
  { id: 'quiet', label: 'Phòng phía sau yên', value: ACTING_ROOM_STILL, applicability: { stance: ['doorway', 'standing'], visualContext: ACTING_VISUAL_STAND } },
  { id: 'solo', label: 'Chỉ nhân vật chính chuyển động', value: 'Only he moves.', applicability: { stance: ['doorway', 'standing'], visualContext: ACTING_VISUAL_STAND } },
  { id: 'sitstill', label: 'Phòng yên, chỉ chuyển động nhẹ', value: ACTING_ROOM_SIT_STILL, applicability: { stance: ['sitting'], visualContext: ACTING_VISUAL_SIT } },
];

export { ACTING_EMOTIONS };

export function actingVisualBlob(shot: Pick<FamixaSeriesShot, 'story' | 'visual' | 'motionPromptVi' | 'beatText'>) {
  return `${shot.story || ''} ${shot.visual || ''} ${shot.motionPromptVi || ''} ${shot.beatText || ''}`;
}

export function actingShotStanceOf(shot: Pick<FamixaSeriesShot, 'story' | 'visual' | 'motionPromptVi' | 'beatText'>): ActingShotStance {
  const blob = actingVisualBlob(shot);
  if (ACTING_VISUAL_SIT.test(blob)) return 'sitting';
  if (ACTING_VISUAL_DOOR.test(blob)) return 'doorway';
  if (/đứng|standing/i.test(blob)) return 'standing';
  return 'unknown';
}

export function chipAppliesToShot(chip: ActingCatalogChip, shot: FamixaSeriesShot) {
  const app = chip.applicability;
  if (!app) return true;
  const blob = actingVisualBlob(shot);
  if (app.visualContext && !app.visualContext.test(blob)) return false;
  if (app.stance?.length && !app.stance.includes(actingShotStanceOf(shot))) return false;
  return true;
}

export function actingChipsForShot(shot: FamixaSeriesShot, chips: readonly ActingCatalogChip[]) {
  return chips.filter((chip) => chipAppliesToShot(chip, shot));
}

export function actingRoomStillForSpeaker(name?: string) {
  const who = (name || '').trim();
  if (!who) return ACTING_ROOM_SIT_STILL;
  return `The living room remains still; only ${who} makes subtle movement.`;
}

/** Spoken-shot speaker from the current Dialogue/Line/Cue — not roster order. */
export function shotActingSpeakerOf(
  state: {
    characters?: { id?: string; name?: string }[];
    roles?: { characterId?: string; name?: string; title?: string }[];
  },
  _shot: FamixaSeriesShot,
  lines: Array<{ characterId?: string; name?: string }>,
): { characterId?: string; name: string } {
  const line = lines[0];
  if (!line?.characterId) return { name: '' };
  const id = line.characterId;
  const fromLine = (line.name || '').trim();
  const fromChar = (state.characters ?? []).find((c) => c.id === id)?.name?.trim();
  const fromRole = (state.roles ?? []).find((r) => r.characterId === id);
  const fromRoleName = (fromRole?.name || fromRole?.title || '').trim();
  return { characterId: id, name: fromLine || fromChar || fromRoleName };
}

export function actingBeatFitsShotContext(shot: FamixaSeriesShot, beat?: ShotActingBeat) {
  const row = beat ?? shot.actingBeat;
  if (!row) return true;
  const stance = actingShotStanceOf(shot);
  const blob = actingVisualBlob(shot);
  const action = row.before.action || '';
  const gaze = row.before.gaze || '';
  const body = row.before.body || '';
  if (stance === 'sitting') {
    if (/doorway|Standing at the living-room/i.test(action)) return false;
    if (/toward (?:his )?mother|looks toward his mother/i.test(gaze) && !ACTING_VISUAL_MOM.test(blob)) return false;
    if (/half-step|takes a small half-step/i.test(body)) return false;
  }
  if ((stance === 'doorway' || stance === 'standing') && /Sitting on the sofa/i.test(action) && !ACTING_VISUAL_SIT.test(blob)) {
    return false;
  }
  return true;
}

export function actingBeatContextStatus(shot: FamixaSeriesShot, beat?: ShotActingBeat) {
  const row = beat ?? shot.actingBeat;
  if (!row) return 'MISSING';
  return actingBeatFitsShotContext(shot, row) ? 'OK' : INVALID_CONTEXTUAL_BEAT;
}

export function derivedPaperProp(shot: FamixaSeriesShot): string | undefined {
  const existing = shot.actingBeat?.before.prop?.trim();
  if (existing === ACTING_PAPER_PROP_EN) return existing;
  const blob = actingVisualBlob(shot);
  if (ACTING_VISUAL_PAPER.test(`${blob} ${existing || ''}`)) return ACTING_PAPER_PROP_EN;
  return existing || undefined;
}

export function derivedActingProp(shot: FamixaSeriesShot): string | undefined {
  const paper = derivedPaperProp(shot);
  if (paper === ACTING_PAPER_PROP_EN) return paper;
  const existing = shot.actingBeat?.before.prop?.trim();
  if (existing === ACTING_PHONE_PROP_EN) return existing;
  const blob = actingVisualBlob(shot);
  if (ACTING_VISUAL_PHONE.test(blob) && !ACTING_VISUAL_PAPER.test(blob)) return ACTING_PHONE_PROP_EN;
  return existing || paper;
}

export function actingEmotionOf(shot: FamixaSeriesShot): ActingEmotion | undefined {
  const raw = (shot.actingBeat?.during.emotion || '').trim();
  return ACTING_EMOTIONS.find((e) => e === raw);
}

export const ACTING_BEAT_UNSAVED_LABEL = 'Có thay đổi chưa lưu';
export const ACTING_BEAT_SAVED_LABEL = 'Đã lưu';
export const ACTING_BEAT_SAVE_LABEL = 'Lưu diễn xuất';
export const ACTING_BEAT_UNSAVED_LEAVE = 'Có thay đổi chưa lưu. Lưu diễn xuất trước khi rời?';
export const ACTING_BEAT_INVALID_CONTEXT = 'Beat không khớp hình shot hiện tại. Sửa rồi Lưu — không tự ghi đè.';
export const ACTING_BEAT_APPLY_VISUAL = 'Dùng beat từ hình';

export type ActingBeatEditorHandle = {
  dirty: () => boolean;
  save: () => void;
};

/** Editable Director fields only. holdSec / intensity / pace are not in this editor. */
export function actingBeatEditableSnapshot(beat?: ShotActingBeat) {
  return {
    action: beat?.before.action ?? '',
    body: beat?.before.body ?? '',
    gaze: beat?.before.gaze ?? '',
    room: beat?.before.room ?? '',
    emotion: beat?.during.emotion ?? '',
    afterAction: beat?.after.action ?? '',
    afterGaze: beat?.after.gaze ?? '',
  };
}

export function actingBeatDirty(saved?: ShotActingBeat, draft?: ShotActingBeat) {
  return JSON.stringify(actingBeatEditableSnapshot(saved)) !== JSON.stringify(actingBeatEditableSnapshot(draft));
}

export function confirmLeaveActingBeat(dirty: boolean, confirmFn: (message: string) => boolean) {
  if (!dirty) return true;
  return confirmFn(ACTING_BEAT_UNSAVED_LEAVE);
}

export function patchActingBeat(
  shot: FamixaSeriesShot,
  spoken: boolean,
  patch: { emotion?: string; action?: string; body?: string; gaze?: string; room?: string },
): ShotActingBeat {
  const prev = shot.actingBeat;
  return {
    before: {
      action: patch.action ?? prev?.before.action,
      body: patch.body ?? prev?.before.body,
      prop: derivedActingProp(shot) ?? prev?.before.prop,
      gaze: patch.gaze ?? prev?.before.gaze,
      room: patch.room ?? prev?.before.room,
      holdSec: prev?.before.holdSec ?? SPEECH_LEAD_IN,
    },
    during: {
      speech: spoken || Boolean(prev?.during.speech),
      emotion: patch.emotion ?? prev?.during.emotion,
      intensity: prev?.during.intensity,
      pace: prev?.during.pace,
    },
    after: {
      action: prev?.after.action,
      gaze: prev?.after.gaze,
      holdSec: prev?.after.holdSec ?? SPEECH_TAIL,
    },
  };
}
