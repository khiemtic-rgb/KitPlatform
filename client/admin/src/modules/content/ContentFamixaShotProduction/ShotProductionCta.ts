/** Director / staff CTAs. Keep out of .tsx so Vite Fast Refresh can reload the desk. */

import { DIRECTOR_SHOT_COMMAND_LABEL } from '../kit-video-production-ux';
import type { SeriesShotRun } from '../content-famixa-series';
import { PICTURE_DUPLICATE_COPY } from '../content-famixa-picture-pixel-invariant';
import { directorRecoveryOf } from './ShotProductionDirector';
import { nextShotProductionCommand, staffCommandCta, type ShotProductionCommand } from './ShotProductionOrchestrator';
import type { ShotProductionSnapshot } from './ShotProductionState';

export type StaffShotAction =
  | 'produce'
  | 'approve-picture'
  | 'approve-video'
  | 'use-current-take'
  | 'motion'
  | 'lipsync'
  | 'mix'
  | 'watch'
  | 'qa'
  | 'blocked'
  | 'accept-existing'
  | 'edit-input'
  | 'picture'
  | 'retry-camera';

export const DIRECTOR_CURRENT_TAKE_NOTE = 'Video hiện tại đã khớp hình và có thể sử dụng.';

function directorVisibleTakeUrl(snap: ShotProductionSnapshot) {
  return (
    (snap.visibleTake?.url || '').trim() ||
    (snap.execution?.currentArtifacts.motion?.url || '').trim() ||
    (snap.execution?.visibleArtifacts.motion?.url || '').trim()
  );
}

/** Desk-only. Does not change nextShotProductionCommand or ACCEPT_EXISTING. */
export function directorCanApproveCurrentTake(snap: ShotProductionSnapshot) {
  const motionCurrent = snap.execution?.currentArtifacts.motion?.validity === 'CURRENT';
  return Boolean(motionCurrent && directorVisibleTakeUrl(snap) && !snap.videoApproved);
}

export function shotPipelineBusy(opts: {
  shotId: string;
  busyShotId?: string;
  motionBusyId?: string;
  lipsyncBusyId?: string;
}) {
  const motionBusy = opts.motionBusyId === opts.shotId;
  const lipsyncBusy = opts.lipsyncBusyId === opts.shotId;
  const pictureBusy = Boolean(opts.busyShotId === opts.shotId && !motionBusy && !lipsyncBusy);
  return { pictureBusy, motionBusy, lipsyncBusy };
}

export function staffPrimaryCta(snap: ShotProductionSnapshot) {
  return staffCommandCta(nextShotProductionCommand(snap));
}

/** Director note authority = execution + canonical command. Diagnostic flags are not copy sources. */
export function directorExecutionNote(opts: { snap: ShotProductionSnapshot; cmd: ShotProductionCommand }) {
  const motionVal = opts.snap.execution?.currentArtifacts.motion?.validity;
  const acceptedVal = opts.snap.execution?.acceptedArtifacts?.motion?.validity;
  const type = opts.cmd.type;
  if (motionVal === 'CURRENT' && (type === 'ENSURE_VOICE' || type === 'WAIT_VOICE')) {
    return 'Video hiện tại đã khớp hình. Tiếp tục phần thoại.';
  }
  if (directorCanApproveCurrentTake(opts.snap) && type !== 'ENSURE_VOICE' && type !== 'WAIT_VOICE') {
    return DIRECTOR_CURRENT_TAKE_NOTE;
  }
  if (type === 'CONFIRM_MOTION' && motionVal !== 'CURRENT') {
    return 'Hình đã duyệt. Video hiện tại không còn khớp hình.';
  }
  if (acceptedVal === 'ACCEPTED_STALE' && type !== 'CONFIRM_MOTION') {
    return opts.snap.execution?.reason || 'Video đã được chấp nhận. Tiếp tục bước sản xuất tiếp theo.';
  }
  return opts.snap.execution?.reason || '';
}

export function directorPictureVideoSurface(opts: {
  snap: ShotProductionSnapshot;
  cmd: ShotProductionCommand;
  hasLiveStill: boolean;
  kfApproved: boolean;
  duplicatePixel?: boolean;
}) {
  if (opts.duplicatePixel) {
    return {
      pendingApproval: false,
      phase: 'picture' as const,
      note: PICTURE_DUPLICATE_COPY,
      primary: { label: 'Tạo hình mới', action: 'picture' as const },
      secondary: undefined,
    };
  }
  const approved = Boolean(opts.kfApproved || opts.snap.keyframeApproved);
  const pendingApproval = Boolean(opts.hasLiveStill && !approved && !opts.snap.pictureUnusable);
  const needPicture = Boolean(!opts.hasLiveStill && (!opts.snap.keyframeReady || opts.snap.pictureUnusable));
  if (pendingApproval) {
    return {
      pendingApproval: true,
      phase: 'picture' as const,
      note: 'Ảnh này chưa duyệt. Bấm Duyệt hình — chưa tạo video.',
      primary: { label: 'Duyệt hình', action: 'approve-picture' as const },
      secondary: { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  if (needPicture || opts.cmd.type === 'ENSURE_PICTURE' || opts.cmd.type === 'WAIT_PICTURE') {
    return {
      pendingApproval: false,
      phase: 'picture' as const,
      note: 'Chưa có hình. Bấm Tạo hình.',
      primary: { label: 'Tạo hình', action: 'picture' as const },
      secondary: undefined,
    };
  }
  if (opts.cmd.type === 'WAIT_PICTURE_APPROVAL') {
    return {
      pendingApproval: true,
      phase: 'picture' as const,
      note: 'Ảnh này chưa duyệt. Bấm Duyệt hình — chưa tạo video.',
      primary: { label: 'Duyệt hình', action: 'approve-picture' as const },
      secondary: { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  const exec = opts.snap.execution;
  const motionCurrent = exec?.currentArtifacts.motion?.validity === 'CURRENT';
  const note = directorExecutionNote({ snap: opts.snap, cmd: opts.cmd });
  if (
    directorCanApproveCurrentTake(opts.snap) &&
    opts.cmd.type !== 'ENSURE_VOICE' &&
    opts.cmd.type !== 'WAIT_VOICE'
  ) {
    return {
      pendingApproval: false,
      phase: 'motion' as const,
      note,
      primary: { label: 'Dùng video này', action: 'use-current-take' as const },
      secondary: { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  if (opts.cmd.type === 'CONFIRM_MOTION' && !motionCurrent && !opts.snap.motionBusy) {
    return {
      pendingApproval: false,
      phase: 'motion' as const,
      note,
      primary: { label: 'Tạo video', action: 'motion' as const },
      secondary: { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  return {
    pendingApproval: false,
    phase: undefined,
    note,
    primary: undefined,
    secondary: { label: 'Tạo hình mới', action: 'picture' as const },
  };
}

export function directorPrimaryCta(snap: ShotProductionSnapshot) {
  const recovery = directorRecoveryOf(snap);
  if (recovery.status === 'PICTURE_REQUIRED') {
    return { label: 'Tạo hình mới', action: 'picture' as const };
  }
  if (recovery.status === 'RETRY_LOCKED') {
    return { label: 'Tạo video', action: 'motion' as const };
  }
  const cmd = nextShotProductionCommand(snap);
  const staff = staffCommandCta(cmd);
  if (cmd.type === 'ENSURE_VOICE' && snap.voiceDurationSec > 0.2) {
    return { ...staff, label: 'Nạp thoại' };
  }
  return { ...staff, label: DIRECTOR_SHOT_COMMAND_LABEL[cmd.type] || staff.label };
}

/** Studio CTA only. Reuses startSceneTurbo remake. Does not restamp or touch batch I2V. */
export function studioMotionSendOpts(snap: ShotProductionSnapshot): { remake?: boolean } {
  if (nextShotProductionCommand(snap).type !== 'CONFIRM_MOTION') return {};
  if (snap.retryLocked || snap.execution?.gates.sameFailedInput) return {};
  if (snap.execution?.currentArtifacts.motion?.validity === 'CURRENT') return {};
  if (snap.execution?.acceptedArtifacts.motion?.validity === 'ACCEPTED_STALE') return {};
  if (snap.visibleTake?.url || snap.execution?.lastSuccessArtifacts.motion?.url) return { remake: true };
  return {};
}

/** Studio CTA only. Reuses startLipsync remake. Does not change Fal / timing. */
export function studioLipsyncSendOpts(snap: ShotProductionSnapshot): { remake?: boolean } {
  if (nextShotProductionCommand(snap).type !== 'CONFIRM_LIPSYNC') return {};
  if (snap.voiceStale || snap.takeFromOtherPicture) return { remake: true };
  if (!snap.lipSyncStale || !snap.input.hasLipSync) return {};
  return { remake: true };
}

export function shotQaMissing(
  shotQa: SeriesShotRun['shotQa'] | undefined,
  spoken: boolean,
  opts?: { afterLipsync?: boolean; afterMix?: boolean },
) {
  const missing: string[] = [];
  if (!shotQa?.action) missing.push('Hành động phù hợp');
  if (!shotQa?.continuity) missing.push('Continuity ổn');
  if (spoken && !shotQa?.voiceFace) missing.push('Giọng / khuôn mặt phù hợp');
  if (spoken && opts?.afterLipsync && !shotQa?.lipsyncQuality) missing.push('Lip-sync đạt');
  if (spoken && (opts?.afterMix || opts?.afterLipsync) && !shotQa?.finalAv) missing.push('Final A/V đạt');
  return missing;
}

export function shotStaleNotes(snap: ShotProductionSnapshot) {
  const notes: string[] = [];
  if (snap.voiceStale) notes.push('Thoại đã thay đổi. Shot cần cập nhật.');
  if (snap.keyframeStale) notes.push('Hành động đã thay đổi. Hình ảnh và chuyển động cần được cập nhật.');
  if (snap.motionStale && !snap.keyframeStale) notes.push('Chuyển động không còn phù hợp.');
  if (snap.lipSyncStale) notes.push('Video Lip-sync cần được cập nhật.');
  if (snap.mixStale) notes.push('Video hoàn chỉnh cần được cập nhật.');
  return notes;
}
