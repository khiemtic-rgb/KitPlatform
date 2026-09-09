import { retryTouches } from '../famixa-video-audio-lipsync-pipeline';
import { sameFailedInput } from '../content-famixa-runway-pipe';

export type ShotProductionEdit =
  | 'dialogue'
  | 'voiceId'
  | 'acting'
  | 'action'
  | 'blocking'
  | 'character'
  | 'visualMode';

export type ShotRetryStage = 'voice' | 'picture' | 'motion' | 'lipsync' | 'mix';

export type Invalidation = {
  voice: boolean;
  picture: boolean;
  motion: boolean;
  lipsync: boolean;
  mix: boolean;
  visualReview: boolean;
  block?: 'CHARACTER_AUTHORITY_REQUIRED' | 'VISUAL_MODE_AUTHORITY_REQUIRED';
};

export function invalidateAfterEdit(change: ShotProductionEdit): Invalidation {
  if (change === 'character') {
    return { voice: false, picture: false, motion: false, lipsync: false, mix: false, visualReview: false, block: 'CHARACTER_AUTHORITY_REQUIRED' };
  }
  if (change === 'visualMode') {
    return { voice: false, picture: false, motion: false, lipsync: false, mix: false, visualReview: false, block: 'VISUAL_MODE_AUTHORITY_REQUIRED' };
  }
  if (change === 'dialogue') {
    return { voice: true, picture: false, motion: false, lipsync: true, mix: true, visualReview: false };
  }
  if (change === 'voiceId') {
    return { voice: true, picture: false, motion: false, lipsync: true, mix: true, visualReview: false };
  }
  if (change === 'acting') {
    return { voice: true, picture: false, motion: true, lipsync: true, mix: true, visualReview: true };
  }
  if (change === 'blocking') {
    return { voice: false, picture: true, motion: true, lipsync: true, mix: true, visualReview: false };
  }
  return { voice: false, picture: true, motion: true, lipsync: true, mix: true, visualReview: false };
}

export function retryScope(stage: ShotRetryStage) {
  if (stage === 'picture') {
    return { voice: false, keyframe: true, i2v: false, lipsync: false, mix: false };
  }
  if (stage === 'motion') {
    const t = retryTouches('i2v');
    return { voice: false, keyframe: false, i2v: t.i2v, lipsync: false, mix: false };
  }
  if (stage === 'voice') {
    const t = retryTouches('voice');
    return { voice: t.voice, keyframe: false, i2v: false, lipsync: false, mix: false };
  }
  if (stage === 'lipsync') {
    const t = retryTouches('lipsync');
    return { voice: false, keyframe: false, i2v: false, lipsync: t.lipsync, mix: false };
  }
  const t = retryTouches('mix');
  return { voice: false, keyframe: false, i2v: false, lipsync: false, mix: t.mix };
}

export function sameFingerprintBlindRetry(opts: { lastFailHash?: string; nextHash?: string; failed?: boolean }) {
  if (!opts.failed) return false;
  const a = (opts.lastFailHash || '').trim();
  const b = (opts.nextHash || '').trim();
  return Boolean(a && b && a === b);
}

export function motionBlindRetryBlocked(opts: { failedKfHash?: string; failedPromptHash?: string; kfHash?: string; promptHash?: string }) {
  return sameFailedInput(
    { failedKfHash: opts.failedKfHash, failedPromptHash: opts.failedPromptHash },
    opts.kfHash,
    opts.promptHash,
  );
}
