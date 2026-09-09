import { sameFingerprintBlindRetry } from './ShotProductionActions';
import { ORCH_GATE } from './ShotProductionErrors';
import type { ShotProductionSnapshot } from './ShotProductionState';

export type ShotProductionCommandKind = 'ENSURE' | 'WAIT' | 'APPROVAL_REQUIRED' | 'CONFIRM_REQUIRED' | 'BLOCK' | 'READY';

export type ShotProductionCommandType =
  | 'BLOCK'
  | 'ENSURE_VOICE'
  | 'WAIT_VOICE'
  | 'ENSURE_PICTURE'
  | 'WAIT_PICTURE'
  | 'WAIT_PICTURE_APPROVAL'
  | 'CONFIRM_MOTION'
  | 'ACCEPT_EXISTING'
  | 'EDIT_INPUT'
  | 'WAIT_MOTION'
  | 'WAIT_VIDEO_REVIEW'
  | 'LIPSYNC_QA_REQUIRED'
  | 'CONFIRM_LIPSYNC'
  | 'WAIT_LIPSYNC'
  | 'ENSURE_MIX'
  | 'WAIT_MIX'
  | 'READY_FINAL';

export type ShotProductionCommand = {
  type: ShotProductionCommandType;
  kind: ShotProductionCommandKind;
  code?: string;
  paid?: boolean;
  retryStage?: 'voice' | 'picture' | 'motion' | 'lipsync' | 'mix';
};

export type ShotProductionAdapters = {
  ensureVoice: () => Promise<void>;
  ensurePicture: () => Promise<void>;
  ensureMotion: () => Promise<void>;
  ensureLipsync: () => Promise<void>;
  ensureMix: () => Promise<void>;
};

/** takeFromOtherPicture is diagnostic input only. CURRENT / ACCEPTED_STALE / sameFailedInput win. */
function executionAllowsMotionRemake(snap: ShotProductionSnapshot) {
  const motionVal = snap.execution?.currentArtifacts.motion?.validity;
  const acceptedVal = snap.execution?.acceptedArtifacts.motion?.validity;
  if (motionVal === 'CURRENT') return false;
  if (acceptedVal === 'ACCEPTED_STALE' || acceptedVal === 'CURRENT') return false;
  if (snap.acceptedTake?.url) return false;
  if (snap.execution?.gates.sameFailedInput || snap.retryLocked) return false;
  return true;
}

export function nextShotProductionCommand(
  snap: ShotProductionSnapshot,
  opts?: { confirmedMotion?: boolean; confirmedLipsync?: boolean; lastFailHash?: string; nextHash?: string },
): ShotProductionCommand {
  const fail = {
    failed: snap.motionFailed || snap.lipsyncFailed || snap.mixFailed,
    lastFailHash: opts?.lastFailHash || snap.lastFailHash,
    nextHash: opts?.nextHash || (snap.motionFailed ? snap.fps.motion : snap.lipsyncFailed ? snap.fps.lipsync : snap.fps.mix),
  };
  if (snap.intent.productionMode === 'MULTI_SPEAKER_BLOCKED') {
    return { type: 'BLOCK', kind: 'BLOCK', code: ORCH_GATE.MULTI_SPEAKER_LIPSYNC_UNSUPPORTED };
  }
  if (snap.blockingReason === ORCH_GATE.CHARACTER_AUTHORITY_REQUIRED) {
    return { type: 'BLOCK', kind: 'BLOCK', code: ORCH_GATE.CHARACTER_AUTHORITY_REQUIRED };
  }
  if (snap.intent.productionMode === 'SPOKEN' && snap.input.cues.some((c) => !(c.voiceId || '').trim())) {
    return { type: 'BLOCK', kind: 'BLOCK', code: ORCH_GATE.VOICE_NOT_ASSIGNED };
  }
  if (snap.stage === 'NEEDS_VISUAL_REVIEW') {
    return { type: 'BLOCK', kind: 'BLOCK', code: ORCH_GATE.NEEDS_VISUAL_REVIEW, retryStage: 'picture' };
  }
  if (snap.finalReady && snap.finalArtifactReady) return { type: 'READY_FINAL', kind: 'READY' };
  if (!snap.isSilent && !snap.voiceReady) {
    return snap.stage === 'VOICE_REQUIRED'
      ? { type: 'ENSURE_VOICE', kind: 'ENSURE', retryStage: 'voice' }
      : { type: 'WAIT_VOICE', kind: 'WAIT' };
  }
  if (!snap.keyframeReady || snap.pictureUnusable) return { type: 'ENSURE_PICTURE', kind: 'ENSURE', retryStage: 'picture' };
  if (snap.picturePendingApproval || !snap.keyframeApproved) {
    return { type: 'WAIT_PICTURE_APPROVAL', kind: 'APPROVAL_REQUIRED' };
  }
  if (
    executionAllowsMotionRemake(snap) &&
    snap.takeFromOtherPicture &&
    snap.keyframeApproved &&
    !snap.motionBusy
  ) {
    return { type: 'CONFIRM_MOTION', kind: 'CONFIRM_REQUIRED', paid: true, code: ORCH_GATE.CONFIRM_MOTION, retryStage: 'motion' };
  }
  if (snap.durationMismatch) {
    return {
      type: 'BLOCK',
      kind: 'BLOCK',
      code:
        snap.blockingReason === ORCH_GATE.PRODUCTION_SHORTER_THAN_DIALOGUE ||
        snap.blockingReason === ORCH_GATE.PRODUCTION_LONGER_THAN_PERFORMANCE ||
        snap.blockingReason === ORCH_GATE.PRODUCTION_BELOW_FLOOR
          ? snap.blockingReason
          : ORCH_GATE.DURATION_MISMATCH,
    };
  }
  if (snap.motionBusy) return { type: 'WAIT_MOTION', kind: 'WAIT' };
  const hasVisible = Boolean(snap.visibleTake?.url);
  const accepted = Boolean(snap.acceptedTake?.url);
  const currentFailed = snap.currentAttempt?.status === 'FAILED' || snap.motionFailed;
  if (hasVisible && !accepted && executionAllowsMotionRemake(snap) && snap.takeFromOtherPicture && snap.keyframeApproved) {
    return { type: 'CONFIRM_MOTION', kind: 'CONFIRM_REQUIRED', paid: true, code: ORCH_GATE.CONFIRM_MOTION, retryStage: 'motion' };
  }
  if (hasVisible && !accepted && (currentFailed || snap.motionStale)) {
    return { type: 'ACCEPT_EXISTING', kind: 'CONFIRM_REQUIRED' };
  }
  if (sameFingerprintBlindRetry(fail) && !hasVisible) {
    return { type: 'EDIT_INPUT', kind: 'BLOCK', code: ORCH_GATE.DO_NOT_BLIND_RETRY };
  }
  if (snap.retryLocked && !hasVisible && !accepted) {
    return { type: 'EDIT_INPUT', kind: 'BLOCK', code: ORCH_GATE.DO_NOT_BLIND_RETRY };
  }
  if (!snap.motionReady && !accepted) {
    if (snap.retryLocked) return { type: 'EDIT_INPUT', kind: 'BLOCK', code: ORCH_GATE.DO_NOT_BLIND_RETRY };
    return { type: 'CONFIRM_MOTION', kind: 'CONFIRM_REQUIRED', paid: true, code: ORCH_GATE.CONFIRM_MOTION, retryStage: 'motion' };
  }
  if (!snap.videoApproved && !accepted) return { type: 'WAIT_VIDEO_REVIEW', kind: 'APPROVAL_REQUIRED' };
  if (!snap.isSilent && !snap.qaReady) {
    return { type: 'LIPSYNC_QA_REQUIRED', kind: 'APPROVAL_REQUIRED', code: ORCH_GATE.LIPSYNC_QA_REQUIRED };
  }
  if (!snap.isSilent && snap.lipsyncBusy) return { type: 'WAIT_LIPSYNC', kind: 'WAIT' };
  if (!snap.isSilent && !snap.lipSyncReady) {
    return { type: 'CONFIRM_LIPSYNC', kind: 'CONFIRM_REQUIRED', paid: true, code: ORCH_GATE.CONFIRM_LIPSYNC, retryStage: 'lipsync' };
  }
  if (snap.mixBusy) return { type: 'WAIT_MIX', kind: 'WAIT' };
  if (!snap.mixReady) return { type: 'ENSURE_MIX', kind: 'ENSURE', retryStage: 'mix' };
  if (!snap.isSilent && !snap.qualityPassed) {
    return { type: 'LIPSYNC_QA_REQUIRED', kind: 'APPROVAL_REQUIRED', code: ORCH_GATE.LIPSYNC_QA_REQUIRED };
  }
  if (!snap.finalArtifactReady) return { type: 'ENSURE_MIX', kind: 'ENSURE', retryStage: 'mix' };
  return { type: 'READY_FINAL', kind: 'READY' };
}

export async function advanceShotProduction(
  snap: ShotProductionSnapshot,
  adapters: ShotProductionAdapters,
  opts?: { confirmedMotion?: boolean; confirmedLipsync?: boolean; lastFailHash?: string; nextHash?: string },
) {
  const cmd = nextShotProductionCommand(snap, opts);
  if (cmd.type === 'ENSURE_VOICE') await adapters.ensureVoice();
  if (cmd.type === 'ENSURE_PICTURE') await adapters.ensurePicture();
  if (cmd.type === 'ENSURE_MIX') await adapters.ensureMix();
  if (cmd.type === 'CONFIRM_MOTION' && opts?.confirmedMotion) await adapters.ensureMotion();
  if (cmd.type === 'CONFIRM_LIPSYNC' && opts?.confirmedLipsync) await adapters.ensureLipsync();
  return cmd;
}

const STOP: ShotProductionCommandType[] = [
  'BLOCK',
  'WAIT_VOICE',
  'WAIT_PICTURE',
  'WAIT_PICTURE_APPROVAL',
  'CONFIRM_MOTION',
  'ACCEPT_EXISTING',
  'EDIT_INPUT',
  'WAIT_MOTION',
  'WAIT_VIDEO_REVIEW',
  'LIPSYNC_QA_REQUIRED',
  'CONFIRM_LIPSYNC',
  'WAIT_LIPSYNC',
  'WAIT_MIX',
  'READY_FINAL',
];

export async function produceShot(
  snap: ShotProductionSnapshot,
  adapters: ShotProductionAdapters,
  refresh: () => ShotProductionSnapshot,
  opts?: { confirmedMotion?: boolean; confirmedLipsync?: boolean; lastFailHash?: string; nextHash?: string },
) {
  let current = snap;
  const ran: ShotProductionCommandType[] = [];
  for (let i = 0; i < 8; i++) {
    current = i === 0 ? current : refresh();
    const cmd = await advanceShotProduction(current, adapters, opts);
    ran.push(cmd.type);
    if (STOP.includes(cmd.type)) {
      return { command: cmd, ran, snapshot: refresh() };
    }
    current = refresh();
    const after = nextShotProductionCommand(current, opts);
    if (after.type === cmd.type && cmd.kind === 'ENSURE') {
      const wait =
        cmd.type === 'ENSURE_VOICE'
          ? 'WAIT_VOICE'
          : cmd.type === 'ENSURE_PICTURE'
            ? 'WAIT_PICTURE'
            : 'WAIT_MIX';
      return { command: { type: wait as ShotProductionCommandType, kind: 'WAIT' as const }, ran, snapshot: current };
    }
  }
  return { command: nextShotProductionCommand(refresh(), opts), ran, snapshot: refresh() };
}

export function staffCommandCta(cmd: ShotProductionCommand) {
  if (cmd.type === 'READY_FINAL') return { label: 'Xem video', action: 'watch' as const };
  if (cmd.type === 'WAIT_PICTURE_APPROVAL') return { label: 'Xem & duyệt hình', action: 'approve-picture' as const };
  if (cmd.type === 'WAIT_VIDEO_REVIEW') return { label: 'Duyệt chuyển động', action: 'approve-video' as const };
  if (cmd.type === 'LIPSYNC_QA_REQUIRED') return { label: 'Kiểm tra Shot', action: 'qa' as const, hint: cmd.code };
  if (cmd.type === 'ACCEPT_EXISTING') return { label: 'Dùng video này', action: 'accept-existing' as const };
  if (cmd.type === 'EDIT_INPUT') return { label: 'Chỉnh diễn xuất', action: 'edit-input' as const };
  if (cmd.type === 'CONFIRM_MOTION' || cmd.type === 'WAIT_MOTION') {
    return {
      label: cmd.type === 'WAIT_MOTION' ? 'Đang tạo video...' : 'Tạo chuyển động',
      action: 'motion' as const,
      hint: cmd.type === 'CONFIRM_MOTION' ? ORCH_GATE.CONFIRM_MOTION : undefined,
    };
  }
  if (cmd.type === 'CONFIRM_LIPSYNC' || cmd.type === 'WAIT_LIPSYNC') {
    return {
      label: cmd.type === 'WAIT_LIPSYNC' ? 'Đang tạo Lip-sync...' : 'Tạo Lip-sync',
      action: 'lipsync' as const,
      hint: cmd.type === 'CONFIRM_LIPSYNC' ? ORCH_GATE.CONFIRM_LIPSYNC : undefined,
    };
  }
  if (cmd.type === 'ENSURE_MIX' || cmd.type === 'WAIT_MIX') {
    return { label: cmd.type === 'WAIT_MIX' ? 'Đang hoàn thiện...' : 'Hoàn thiện', action: 'mix' as const };
  }
  if (cmd.type === 'ENSURE_VOICE') return { label: 'Tạo thoại', action: 'produce' as const };
  if (cmd.type === 'ENSURE_PICTURE' || cmd.type === 'WAIT_PICTURE') {
    return { label: cmd.type === 'WAIT_PICTURE' ? 'Đang tạo hình...' : 'Tạo hình', action: 'produce' as const };
  }
  if (cmd.type === 'BLOCK') return { label: 'Xem chi tiết', action: 'blocked' as const, hint: cmd.code };
  return { label: 'Tạo Shot', action: 'produce' as const };
}
