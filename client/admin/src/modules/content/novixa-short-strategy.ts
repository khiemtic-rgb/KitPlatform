import type { ProductionShotActions } from '../../shared/api/content.api';
import type { FamixaSeriesShot } from './content-famixa-series';

/** Same strings the backend gate reads. Blank fields stay on the Famixa contract. */

export const PICTURE_STATIC = 'STATIC';
export const PICTURE_IMAGE_TO_VIDEO = 'IMAGE_TO_VIDEO';
export const PICTURE_MOTION_GRAPHICS = 'MOTION_GRAPHICS';
export const SPEECH_NONE = 'NONE';
export const SPEECH_VOICE_OVER = 'VOICE_OVER';
export const SPEECH_MOUTH_UNSYNCED = 'MOUTH_UNSYNCED';
export const SPEECH_LIP_SYNC = 'LIP_SYNC';

export const STRATEGY_CONFIRMATION_REQUIRED = 'STRATEGY_CONFIRMATION_REQUIRED';
export const STRATEGY_UNDECLARED = 'STRATEGY_UNDECLARED';

export function strategyEngaged(shot?: Pick<FamixaSeriesShot, 'pictureStrategy' | 'speechDelivery'> | null) {
  return Boolean((shot?.pictureStrategy || '').trim() || (shot?.speechDelivery || '').trim());
}

export function strategyClaim(shot?: Pick<FamixaSeriesShot, 'pictureStrategy' | 'speechDelivery' | 'strategyConfirmed' | 'characterIds'> | null) {
  const picture = (shot?.pictureStrategy || '').trim();
  const speech = (shot?.speechDelivery || '').trim();
  if (!picture && !speech) return {};
  const speakers = speech === SPEECH_LIP_SYNC ? (shot?.characterIds?.filter(Boolean).length ?? 0) : 0;
  return {
    pictureStrategy: picture,
    speechDelivery: speech,
    strategyConfirmed: shot?.strategyConfirmed === true,
    directSpeakerCount: speakers > 1 ? speakers : 0,
  };
}

export function isConfirmedStaticVoiceOver(
  shot?: Pick<FamixaSeriesShot, 'pictureStrategy' | 'speechDelivery' | 'strategyConfirmed'> | null,
) {
  return (
    strategyEngaged(shot) &&
    shot?.strategyConfirmed === true &&
    (shot.pictureStrategy || '').trim().toUpperCase() === PICTURE_STATIC &&
    (shot.speechDelivery || '').trim().toUpperCase() === SPEECH_VOICE_OVER
  );
}

/** Confirmed is a server result. A local checkbox is not enough. */
export function backendAcceptedStrategy(actions: ProductionShotActions | undefined, sentConfirmed: boolean) {
  if (!sentConfirmed || !actions?.actions?.length) return false;
  return !actions.actions.some(
    (row) => row.blockReason === STRATEGY_CONFIRMATION_REQUIRED || row.blockReason === STRATEGY_UNDECLARED,
  );
}

export function strategyBlockReason(actions?: ProductionShotActions | null) {
  const row = actions?.actions?.find((item) => {
    const reason = item.blockReason || '';
    return (
      reason === STRATEGY_CONFIRMATION_REQUIRED ||
      reason === STRATEGY_UNDECLARED ||
      reason === 'STATIC_FORBIDS_PAID_VIDEO' ||
      reason === 'MOTION_GRAPHICS_UNSUPPORTED' ||
      reason === 'VOICE_OVER_FORBIDS_LIPSYNC' ||
      reason === 'MOUTH_UNSYNCED_FORBIDS_LIPSYNC' ||
      reason === 'SPEECH_DOES_NOT_REQUIRE_LIPSYNC' ||
      reason === 'LIPSYNC_REQUIRES_IMAGE_TO_VIDEO' ||
      (item.action === 'MIX_VIDEO' && Boolean(reason)) ||
      (item.action === 'CREATE_VIDEO' && Boolean(reason)) ||
      (item.action === 'CREATE_LIPSYNC' && Boolean(reason))
    );
  });
  return row?.blockReason || '';
}
