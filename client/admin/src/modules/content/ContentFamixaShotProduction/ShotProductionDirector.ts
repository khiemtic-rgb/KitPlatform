/** Director-readable recovery. Technical hashes / provider codes stay in Technical mode. */

import { nextShotProductionCommand, type ShotProductionCommandType } from './ShotProductionOrchestrator';
import type { ShotProductionSnapshot } from './ShotProductionState';

export type DirectorActionType =
  | 'ACCEPT_EXISTING'
  | 'EDIT_INPUT'
  | 'RETRY_MOTION'
  | 'ENSURE_VOICE'
  | 'ENSURE_PICTURE'
  | 'QA'
  | 'LIPSYNC'
  | 'CONFIRM_MOTION';

export type DirectorAction = {
  type: DirectorActionType;
  label: string;
  command: ShotProductionCommandType;
};

export type DirectorRecovery = {
  status: string;
  message: string;
  actions: DirectorAction[];
  nextCommand: ShotProductionCommandType;
};

export const DIRECTOR_COPY = {
  SUCCESS_THEN_FAIL:
    'Video mới chưa tạo được.\nVideo trước đó vẫn sẵn sàng.',
  SAME_FAILED_INPUT:
    'Lần gửi trước đã FAIL trên ảnh này.\nTạo video sẽ gửi lệnh đứng yên mới — không tạo ảnh thêm.',
  CAMERAS_EXHAUSTED: 'Hết lệch camera trên ảnh này.\nTạo hình mới rồi duyệt — đừng gửi lại ảnh cũ.',
  PICTURE_CHANGED: 'Hình mới đã duyệt. Video cũ không còn khớp — tạo video từ ảnh này.',
  PROVIDER_FAIL_KEEP_TAKE:
    'Không tạo được video mới.\nVideo trước đó vẫn được giữ nguyên.',
  NO_TAKE: 'Chưa tạo được video cho cảnh này.',
  VOICE_MISSING: 'Chưa nghe được bản thoại trên máy này.',
  VOICE_RELOAD: 'Thoại đã có. Nạp lại bản nghe đã lưu — không tạo TTS mới.',
  QA_REQUIRED: 'Video đã tạo xong.\nHãy kiểm tra trước khi tiếp tục.',
  ACCEPTED_LIPSYNC: 'Video đã được chọn.\nCó thể tiếp tục đồng bộ khẩu hình.',
  QUALITY_QA: 'Video đã lồng tiếng.\nĐánh dấu môi khớp và hình-tiếng trước khi hoàn tất Shot.',
  FINAL_MISSING: 'File hoàn thiện không còn trên máy này.\nBấm Hoàn thiện để ghép lại.',
} as const;

const TECH_LEAK =
  /sameFailedInput|promptHash|kfHash|previewUrl|takeUrl|taskId|INTERNAL\.BAD_OUTPUT|fingerprint|CODE01|h[0-9a-f]{6,}:/i;

export function directorTextIsSafe(text: string) {
  return !TECH_LEAK.test(text);
}

export function normalizeDirectorError(_raw?: string) {
  return 'Không tạo được video mới.';
}

export function directorRecoveryOf(snap: ShotProductionSnapshot): DirectorRecovery {
  const canonical = nextShotProductionCommand(snap);
  const remakePicture: DirectorAction = { type: 'ENSURE_PICTURE', label: 'Tạo hình mới', command: 'ENSURE_PICTURE' };
  const withRemake = (rec: DirectorRecovery): DirectorRecovery =>
    rec.actions.some((a) => a.type === 'ENSURE_PICTURE')
      ? rec
      : { ...rec, actions: [...rec.actions, remakePicture] };
  const finish = (rec: DirectorRecovery): DirectorRecovery => withRemake({ ...rec, nextCommand: canonical.type });

  if (!snap.isSilent && !snap.voiceReady) {
    const saved = snap.voiceDurationSec > 0.2;
    return finish({
      status: 'VOICE_REQUIRED',
      message: saved ? DIRECTOR_COPY.VOICE_RELOAD : DIRECTOR_COPY.VOICE_MISSING,
      actions: [{ type: 'ENSURE_VOICE', label: saved ? 'Nạp thoại' : 'Tạo thoại', command: 'ENSURE_VOICE' }],
      nextCommand: 'ENSURE_VOICE',
    });
  }

  const hasVisible = Boolean(snap.visibleTake?.url);
  const accepted = Boolean(snap.acceptedTake?.url) || snap.videoApproved;
  const currentFailed = snap.currentAttempt?.status === 'FAILED' || snap.motionFailed;
  const locked = snap.retryLocked;

  if (snap.camerasExhausted && currentFailed && !accepted) {
    return finish({
      status: 'PICTURE_REQUIRED',
      message: `${DIRECTOR_COPY.NO_TAKE}\n${DIRECTOR_COPY.CAMERAS_EXHAUSTED}`,
      actions: [remakePicture, { type: 'EDIT_INPUT', label: 'Đổi diễn', command: 'EDIT_INPUT' }],
      nextCommand: 'ENSURE_PICTURE',
    });
  }

  if (locked && currentFailed && !accepted) {
    return finish({
      status: 'RETRY_LOCKED',
      message: hasVisible
        ? `${DIRECTOR_COPY.SUCCESS_THEN_FAIL}\n${DIRECTOR_COPY.SAME_FAILED_INPUT}`
        : `${DIRECTOR_COPY.NO_TAKE}\n${DIRECTOR_COPY.SAME_FAILED_INPUT}`,
      actions: [
        {
          type: 'RETRY_MOTION',
          label: 'Tạo video',
          command: 'CONFIRM_MOTION',
        },
        remakePicture,
        { type: 'EDIT_INPUT', label: 'Đổi diễn', command: 'EDIT_INPUT' },
      ],
      nextCommand: 'CONFIRM_MOTION',
    });
  }

  if (snap.takeFromOtherPicture && snap.keyframeApproved && !snap.picturePendingApproval) {
    return finish({
      status: 'VIDEO_REQUIRED',
      message: DIRECTOR_COPY.PICTURE_CHANGED,
      actions: [{ type: 'CONFIRM_MOTION', label: 'Tạo video', command: 'CONFIRM_MOTION' }],
      nextCommand: 'CONFIRM_MOTION',
    });
  }

  if (hasVisible && snap.takeFromOtherPicture && snap.keyframeApproved && !accepted) {
    return finish({
      status: 'VIDEO_REQUIRED',
      message: DIRECTOR_COPY.PICTURE_CHANGED,
      actions: [
        { type: 'CONFIRM_MOTION', label: 'Tạo video', command: 'CONFIRM_MOTION' },
        { type: 'ACCEPT_EXISTING', label: 'Dùng video này', command: 'ACCEPT_EXISTING' },
        remakePicture,
      ],
      nextCommand: 'CONFIRM_MOTION',
    });
  }

  if (hasVisible && currentFailed && !accepted) {
    const message = locked
      ? `${DIRECTOR_COPY.SUCCESS_THEN_FAIL}\n${DIRECTOR_COPY.SAME_FAILED_INPUT}`
      : DIRECTOR_COPY.PROVIDER_FAIL_KEEP_TAKE;
    const actions: DirectorAction[] = [
      { type: 'ACCEPT_EXISTING', label: 'Dùng video này', command: 'ACCEPT_EXISTING' },
      remakePicture,
      { type: 'EDIT_INPUT', label: 'Đổi diễn', command: 'EDIT_INPUT' },
    ];
    return finish({ status: 'MOTION_FAILED', message, actions, nextCommand: 'ACCEPT_EXISTING' });
  }

  if (!hasVisible && currentFailed) {
    return finish({
      status: 'MOTION_FAILED',
      message: DIRECTOR_COPY.NO_TAKE,
      actions: [{ type: 'RETRY_MOTION', label: 'Tạo lại', command: 'CONFIRM_MOTION' }],
      nextCommand: 'CONFIRM_MOTION',
    });
  }

  if (!hasVisible) {
    return finish({
      status: 'VIDEO_REQUIRED',
      message: '',
      actions: [{ type: 'CONFIRM_MOTION', label: 'Tạo video', command: 'CONFIRM_MOTION' }],
      nextCommand: 'CONFIRM_MOTION',
    });
  }

  if (hasVisible && !accepted && (currentFailed || snap.motionStale)) {
    return finish({
      status: 'ACCEPT_EXISTING',
      message: snap.motionStale ? DIRECTOR_COPY.SUCCESS_THEN_FAIL : '',
      actions: [
        { type: 'ACCEPT_EXISTING', label: 'Dùng video này', command: 'ACCEPT_EXISTING' },
        remakePicture,
        { type: 'EDIT_INPUT', label: 'Đổi diễn', command: 'EDIT_INPUT' },
      ],
      nextCommand: 'ACCEPT_EXISTING',
    });
  }

  if (hasVisible && !accepted) {
    return finish({
      status: 'ACCEPT_EXISTING',
      message: '',
      actions: [
        { type: 'ACCEPT_EXISTING', label: 'Dùng video này', command: 'ACCEPT_EXISTING' },
        remakePicture,
        { type: 'EDIT_INPUT', label: 'Đổi diễn', command: 'EDIT_INPUT' },
      ],
      nextCommand: 'WAIT_VIDEO_REVIEW',
    });
  }

  if (!snap.isSilent && !snap.qaReady) {
    return finish({
      status: 'MOTION_QA',
      message: DIRECTOR_COPY.QA_REQUIRED,
      actions: [
        { type: 'QA', label: 'Kiểm tra video', command: 'LIPSYNC_QA_REQUIRED' },
        remakePicture,
      ],
      nextCommand: 'LIPSYNC_QA_REQUIRED',
    });
  }

  if (!snap.isSilent && !snap.lipSyncReady) {
    return finish({
      status: 'LIPSYNC_READY',
      message: DIRECTOR_COPY.ACCEPTED_LIPSYNC,
      actions: [
        { type: 'LIPSYNC', label: 'Lồng tiếng', command: 'CONFIRM_LIPSYNC' },
        remakePicture,
      ],
      nextCommand: 'CONFIRM_LIPSYNC',
    });
  }

  if (!snap.isSilent && !snap.qualityPassed) {
    return finish({
      status: 'QUALITY_QA',
      message: DIRECTOR_COPY.QUALITY_QA,
      actions: [{ type: 'QA', label: 'Kiểm tra video', command: 'LIPSYNC_QA_REQUIRED' }],
      nextCommand: 'LIPSYNC_QA_REQUIRED',
    });
  }

  if (!snap.mixReady || snap.finalArtifactReady === false) {
    return finish({
      status: 'MIX',
      message: snap.mixReady && snap.finalArtifactReady === false ? DIRECTOR_COPY.FINAL_MISSING : '',
      actions: [],
      nextCommand: 'ENSURE_MIX',
    });
  }

  return finish({
    status: snap.stage,
    message: '',
    actions: [],
    nextCommand: canonical.type,
  });
}
