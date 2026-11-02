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
  | 'retry-camera'
  | 'os-create-video'
  | 'os-create-image'
  | 'os-create-voice'
  | 'os-create-lipsync'
  | 'os-create-mix'
  | 'os-accept'
  | 'os-assemble'
  | 'os-regenerate';

export const DIRECTOR_CURRENT_TAKE_NOTE = 'Video hiện tại đã khớp hình và có thể sử dụng.';
export const DIRECTOR_REMAKE_MOTION_LABEL = 'Tạo lại video';
export const DIRECTOR_LIPSYNC_LABEL = 'Khớp môi';
export const DIRECTOR_LIPSYNC_RESUME_LABEL = 'Hỏi lại môi';
export const DIRECTOR_LIPSYNC_NEW_LABEL = 'Khớp môi mới';

export type DirectorDeskCta = { label: string; action: StaffShotAction };

function directorVisibleTakeUrl(snap: ShotProductionSnapshot) {
  return (
    (snap.visibleTake?.url || '').trim() ||
    (snap.execution?.currentArtifacts?.motion?.url || '').trim() ||
    (snap.execution?.visibleArtifacts?.motion?.url || '').trim()
  );
}

/** Desk Khớp môi. Same-picture take + voice. Does not send Fal. */
export function deskCanOfferLipsync(snap: ShotProductionSnapshot) {
  if (snap.isSilent || snap.lipsyncBusy || snap.takeFromOtherPicture) return false;
  if (!directorVisibleTakeUrl(snap)) return false;
  return Boolean(snap.voiceReady);
}

/** CURRENT + playable LipSync is not a Director production CTA. */
export function deskLipsyncOutputCurrent(snap: ShotProductionSnapshot) {
  const lip = snap.execution?.currentArtifacts.lipsync;
  if (lip?.validity !== 'CURRENT') return false;
  return Boolean((lip.url || '').trim() || snap.input.hasLipSync);
}

/** Offer Khớp môi on the desk. Hidden when LipSync is already CURRENT. */
export function deskExposeLipsyncCta(snap: ShotProductionSnapshot, forceNew?: boolean) {
  if (forceNew) return deskCanOfferLipsync(snap);
  if (deskLipsyncOutputCurrent(snap) && !snap.lipSyncStale) return false;
  return deskCanOfferLipsync(snap);
}

/** Quality remake. CURRENT LipSync stays off the next-step primary. Confirm still required. */
export function deskExposeLipsyncRemakeCta(snap: ShotProductionSnapshot) {
  if (!deskCanOfferLipsync(snap)) return false;
  return deskLipsyncOutputCurrent(snap) && !snap.lipSyncStale;
}

function deskLipsyncRemakeCta(): DirectorDeskCta {
  return { label: DIRECTOR_LIPSYNC_NEW_LABEL, action: 'lipsync' };
}

function deskLipsyncCta(snap?: ShotProductionSnapshot, forceNew?: boolean): DirectorDeskCta {
  return {
    label: forceNew
      ? DIRECTOR_LIPSYNC_NEW_LABEL
      : snap?.lipsyncFailed
        ? DIRECTOR_LIPSYNC_RESUME_LABEL
        : DIRECTOR_LIPSYNC_LABEL,
    action: 'lipsync',
  };
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
  mixBusyId?: string;
}) {
  const motionBusy = opts.motionBusyId === opts.shotId;
  const lipsyncBusy = opts.lipsyncBusyId === opts.shotId;
  const mixBusy = opts.mixBusyId === opts.shotId;
  const pictureBusy = Boolean(opts.busyShotId === opts.shotId && !motionBusy && !lipsyncBusy && !mixBusy);
  return { pictureBusy, motionBusy, lipsyncBusy, mixBusy };
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

/** CREATE_VIDEO CTA is applyProductionOsActionAuthority. This no longer enables Tạo video. */
export function applyOsCreateVideoCta<T extends {
  pendingApproval: boolean;
  phase?: 'picture' | 'motion' | 'finish';
  note: string;
  primary?: DirectorDeskCta;
}>(desk: T): T {
  return desk;
}

export function directorPictureVideoSurface(opts: {
  snap: ShotProductionSnapshot;
  cmd: ShotProductionCommand;
  hasLiveStill: boolean;
  kfApproved: boolean;
  duplicatePixel?: boolean;
  forceNewLipsync?: boolean;
}): {
  pendingApproval: boolean;
  phase?: 'picture' | 'motion' | 'finish';
  note: string;
  technical?: string;
  primary?: DirectorDeskCta;
  secondary?: DirectorDeskCta;
  tertiary?: DirectorDeskCta;
} {
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
  const displayableStill = Boolean(
    opts.hasLiveStill ||
      opts.snap.picturePendingApproval ||
      (opts.snap.keyframeReady && !approved),
  );
  const pendingApproval = Boolean(displayableStill && !approved && !opts.snap.pictureUnusable);
  const needPicture = Boolean(!displayableStill && (!opts.snap.keyframeReady || opts.snap.pictureUnusable));
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
      secondary: { label: DIRECTOR_REMAKE_MOTION_LABEL, action: 'motion' as const },
      tertiary: deskExposeLipsyncCta(opts.snap, opts.forceNewLipsync)
        ? deskLipsyncCta(opts.snap, opts.forceNewLipsync)
        : { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  const voiceAfterBeat =
    (opts.cmd.type === 'ENSURE_VOICE' || opts.cmd.type === 'WAIT_VOICE') &&
    opts.snap.motionStale &&
    !opts.snap.acceptedTake?.url;
  if (
    (opts.cmd.type === 'CONFIRM_MOTION' ||
      opts.cmd.type === 'ACCEPT_EXISTING' ||
      opts.snap.actingBeatPendingMotion ||
      voiceAfterBeat) &&
    !motionCurrent &&
    !opts.snap.motionBusy
  ) {
    const lipsync = deskExposeLipsyncCta(opts.snap, opts.forceNewLipsync);
    return {
      pendingApproval: false,
      phase: 'motion' as const,
      note:
        opts.cmd.type === 'ACCEPT_EXISTING' || opts.snap.actingBeatPendingMotion
          ? note || 'Diễn xuất đã đổi. Tạo video mới — Confirm mới gửi Veo.'
          : note,
      primary: { label: 'Tạo video', action: 'motion' as const },
      secondary:
        opts.cmd.type === 'ACCEPT_EXISTING'
          ? { label: 'Dùng video này', action: 'accept-existing' as const }
          : lipsync
            ? deskLipsyncCta(opts.snap, opts.forceNewLipsync)
            : { label: 'Tạo hình mới', action: 'picture' as const },
      tertiary:
        opts.cmd.type === 'ACCEPT_EXISTING'
          ? lipsync
            ? deskLipsyncCta(opts.snap, opts.forceNewLipsync)
            : { label: 'Tạo hình mới', action: 'picture' as const }
          : lipsync
            ? { label: 'Tạo hình mới', action: 'picture' as const }
            : undefined,
    };
  }
  if (opts.cmd.type === 'LIPSYNC_QA_REQUIRED' && !opts.snap.lipsyncBusy) {
    return {
      pendingApproval: false,
      phase: 'finish' as const,
      note: note || 'Đánh dấu kiểm tra cảnh trước khi khớp môi.',
      primary: deskExposeLipsyncCta(opts.snap, opts.forceNewLipsync)
        ? deskLipsyncCta(opts.snap, opts.forceNewLipsync)
        : { label: 'Kiểm tra Shot', action: 'qa' as const },
      secondary: { label: DIRECTOR_REMAKE_MOTION_LABEL, action: 'motion' as const },
      tertiary: { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  if (opts.cmd.type === 'CONFIRM_LIPSYNC' && !opts.snap.lipsyncBusy) {
    return {
      pendingApproval: false,
      phase: 'finish' as const,
      note: note || 'Video đã chọn. Tiếp theo là khớp môi — chưa ghép tập.',
      primary: deskLipsyncCta(opts.snap, opts.forceNewLipsync),
      secondary: { label: DIRECTOR_REMAKE_MOTION_LABEL, action: 'motion' as const },
      tertiary: { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  if ((opts.cmd.type === 'ENSURE_MIX' || opts.cmd.type === 'WAIT_MIX') && !opts.snap.mixBusy) {
    const lipsync = deskExposeLipsyncCta(opts.snap, opts.forceNewLipsync);
    const remakeLip = deskExposeLipsyncRemakeCta(opts.snap);
    return {
      pendingApproval: false,
      phase: 'finish' as const,
      note: note || 'Bản mix không còn khớp nguồn hiện tại.',
      primary: { label: 'Hoàn thiện Shot', action: 'mix' as const },
      secondary: { label: DIRECTOR_REMAKE_MOTION_LABEL, action: 'motion' as const },
      tertiary: lipsync
        ? deskLipsyncCta(opts.snap, opts.forceNewLipsync)
        : remakeLip
          ? deskLipsyncRemakeCta()
          : { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  if (opts.cmd.type === 'READY_FINAL' && !opts.snap.mixBusy) {
    const remakeMix = Boolean(opts.snap.mixStale || opts.snap.finalArtifactReady === false);
    const lipsync = deskExposeLipsyncCta(opts.snap, opts.forceNewLipsync);
    const remakeLip = deskExposeLipsyncRemakeCta(opts.snap);
    return {
      pendingApproval: false,
      phase: 'finish' as const,
      note: remakeMix ? note || 'Bản mix không còn khớp nguồn hiện tại.' : note || 'Shot đã có video và thoại.',
      primary: remakeMix
        ? { label: 'Hoàn thiện Shot', action: 'mix' as const }
        : { label: 'Xem video', action: 'watch' as const },
      secondary: remakeLip
        ? deskLipsyncRemakeCta()
        : lipsync
          ? deskLipsyncCta(opts.snap, opts.forceNewLipsync)
          : { label: DIRECTOR_REMAKE_MOTION_LABEL, action: 'motion' as const },
      tertiary: remakeLip || lipsync
        ? { label: DIRECTOR_REMAKE_MOTION_LABEL, action: 'motion' as const }
        : { label: 'Tạo hình mới', action: 'picture' as const },
    };
  }
  const lipsync = deskExposeLipsyncCta(opts.snap, opts.forceNewLipsync);
  return {
    pendingApproval: false,
    phase: undefined,
    note,
    primary: lipsync ? deskLipsyncCta(opts.snap, opts.forceNewLipsync) : undefined,
    secondary: { label: 'Tạo hình mới', action: 'picture' as const },
  };
}

export function directorPrimaryCta(snap: ShotProductionSnapshot) {
  if (
    !snap.pictureUnusable &&
    (snap.picturePendingApproval || (snap.keyframeReady && !snap.keyframeApproved))
  ) {
    return { label: 'Duyệt hình', action: 'approve-picture' as const };
  }
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
  const type = nextShotProductionCommand(snap).type;
  if (type !== 'CONFIRM_MOTION' && !snap.actingBeatPendingMotion) return {};
  if (snap.retryLocked || snap.execution?.gates.sameFailedInput) return {};
  if (snap.execution?.currentArtifacts.motion?.validity === 'CURRENT') return {};
  if (snap.execution?.acceptedArtifacts.motion?.validity === 'ACCEPTED_STALE' && !snap.actingBeatPendingMotion) return {};
  if (snap.visibleTake?.url || snap.execution?.lastSuccessArtifacts.motion?.url) return { remake: true };
  return {};
}

/** Studio CTA only. Reuses startLipsync remake. Does not change Fal / timing. */
export function studioLipsyncSendOpts(snap: ShotProductionSnapshot): { remake?: boolean } {
  if (snap.lipsyncFailed) return {};
  const type = nextShotProductionCommand(snap).type;
  if (type === 'CONFIRM_LIPSYNC') {
    if (snap.voiceStale || snap.takeFromOtherPicture) return { remake: true };
    if (!snap.lipSyncStale || !snap.input.hasLipSync) return {};
    return { remake: true };
  }
  if (
    deskCanOfferLipsync(snap) &&
    (type === 'CONFIRM_MOTION' ||
      type === 'LIPSYNC_QA_REQUIRED' ||
      type === 'WAIT_VIDEO_REVIEW' ||
      type === 'ACCEPT_EXISTING' ||
      type === 'ENSURE_MIX' ||
      type === 'READY_FINAL')
  ) {
    return { remake: true };
  }
  return {};
}

export function shotQaMissing(
  shotQa: SeriesShotRun['shotQa'] | undefined,
  spoken: boolean,
  opts?: { afterLipsync?: boolean; afterMix?: boolean },
) {
  const missing: string[] = [];
  if (!shotQa?.action) missing.push('Hành động phù hợp');
  if (!shotQa?.continuity) missing.push('Continuity ổn');
  if (!shotQa?.motion) missing.push('Chuyển động thực hiện nhịp');
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
