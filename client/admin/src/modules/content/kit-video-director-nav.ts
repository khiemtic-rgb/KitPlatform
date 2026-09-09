import { DIRECTOR_SHOT_COMMAND_LABEL, SERIES_STAFF_TABS, type ProductionUserMode, type SeriesStaffTab } from './kit-video-production-ux';
import type { ShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import type { ShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';

export const DIRECTOR_VIDEO_WORKSPACE_ID = 'FAMIXA_DIRECTOR_VIDEO_WORKSPACE_RESTRUCTURE_V1';

export const SERIES_DIRECTOR_TABS = [
  { id: 'script' as const, label: 'Chuyện' },
  { id: 'characters' as const, label: 'Người' },
  { id: 'overview' as const, label: 'Short' },
];

export const DIRECTOR_HIDDEN_PRIMARY_TABS: SeriesStaffTab[] = [
  'scenes',
  'voice',
  'images',
  'video',
  'finish',
  'publish',
];

export function episodeNavTabs(mode: ProductionUserMode) {
  return mode === 'director' ? SERIES_DIRECTOR_TABS : SERIES_STAFF_TABS;
}

export function directorTabOf(tab: SeriesStaffTab): SeriesStaffTab {
  if (tab === 'scenes') return 'script';
  if (tab === 'voice') return 'characters';
  if (tab === 'images' || tab === 'video' || tab === 'finish' || tab === 'publish') return 'overview';
  return tab;
}

export function isDirectorPrimaryTab(tab: SeriesStaffTab) {
  return tab === 'script' || tab === 'characters' || tab === 'overview';
}

export type DirectorStoryActionKind =
  | 'LOCKED'
  | 'PARSE'
  | 'REVIEW_STORY'
  | 'REVIEW_INHERIT'
  | 'LOCK'
  | 'LOCK_GRAPH'
  | 'ASPECT';

export type DirectorStoryAction = {
  kind: DirectorStoryActionKind;
  locked: boolean;
  canLock: boolean;
  reason?: string;
  missing?: string;
  cta: string;
};

export type DirectorStoryInput = {
  body: string;
  sceneCount: number;
  shotCount: number;
  storyReviewed: boolean;
  needsInheritReview: boolean;
  scriptLocked: boolean;
  shotGraphLocked: boolean;
  aspectOk: boolean;
};

export function nextDirectorStoryAction(input: DirectorStoryInput): DirectorStoryAction {
  const hasStory = Boolean(input.body.trim()) || input.sceneCount > 0 || input.shotCount > 0;
  if (input.scriptLocked && input.shotGraphLocked && input.aspectOk) {
    return { kind: 'LOCKED', locked: true, canLock: false, cta: 'Đã khóa chuyện' };
  }
  if (!hasStory) {
    return {
      kind: 'PARSE',
      locked: false,
      canLock: false,
      reason: 'Chưa thể khóa chuyện.',
      missing: 'Tách cảnh từ nội dung.',
      cta: 'Nhận pack',
    };
  }
  if (input.sceneCount > 0 && !input.storyReviewed && !input.scriptLocked) {
    return {
      kind: 'REVIEW_STORY',
      locked: false,
      canLock: false,
      reason: 'Chưa thể khóa chuyện.',
      missing: 'Duyệt chuyện đã tách.',
      cta: 'Duyệt chuyện',
    };
  }
  if (input.needsInheritReview && !input.scriptLocked) {
    return {
      kind: 'REVIEW_INHERIT',
      locked: false,
      canLock: false,
      reason: 'Chưa thể khóa chuyện.',
      missing: 'Duyệt kế thừa tập trước.',
      cta: 'Duyệt kế thừa',
    };
  }
  if (!input.scriptLocked) {
    return { kind: 'LOCK', locked: false, canLock: true, cta: 'Khóa chuyện' };
  }
  if (!input.shotGraphLocked) {
    return {
      kind: 'LOCK_GRAPH',
      locked: false,
      canLock: false,
      reason: 'Chưa thể khóa chuyện.',
      missing: 'Duyệt cách chia shot.',
      cta: 'Duyệt chia shot',
    };
  }
  if (!input.aspectOk) {
    return {
      kind: 'ASPECT',
      locked: false,
      canLock: false,
      reason: 'Chưa thể khóa chuyện.',
      missing: 'Chọn khung xuất 16:9 hoặc 9:16.',
      cta: 'Chọn khung 16:9',
    };
  }
  return { kind: 'LOCKED', locked: true, canLock: false, cta: 'Đã khóa chuyện' };
}

export type DirectorLaneMark = '✓' | '●' | '○' | '—';

export function directorShotLaneMarks(snap: ShotProductionSnapshot) {
  const picture: DirectorLaneMark = snap.keyframeApproved
    ? '✓'
    : snap.keyframeReady || snap.picturePendingApproval
      ? '●'
      : '○';
  const video: DirectorLaneMark =
    snap.finalReady || snap.motionReady
      ? '✓'
      : snap.motionBusy || Boolean(snap.visibleTake?.url)
        ? '●'
        : '○';
  const voice: DirectorLaneMark = snap.isSilent ? '—' : snap.voiceReady ? '✓' : '●';
  return { picture, video, voice };
}

export function directorShotAttention(
  cmd: ShotProductionCommand,
  snap: ShotProductionSnapshot,
): { mark: '✓' | '●' | '○'; label: string } {
  if (snap.finalReady || cmd.type === 'READY_FINAL') return { mark: '✓', label: 'Đạt' };
  if (cmd.type === 'ENSURE_PICTURE') return { mark: '○', label: 'Chưa tạo hình' };
  if (cmd.type === 'LIPSYNC_QA_REQUIRED') return { mark: '●', label: 'Cần kiểm tra video' };
  if (cmd.type === 'WAIT_PICTURE_APPROVAL') return { mark: '●', label: 'Duyệt hình' };
  if (cmd.type === 'WAIT_VIDEO_REVIEW') return { mark: '●', label: 'Duyệt video' };
  if (cmd.type === 'CONFIRM_MOTION') return { mark: '○', label: 'Chưa tạo video' };
  if (cmd.type === 'CONFIRM_LIPSYNC') return { mark: '●', label: 'Lồng tiếng' };
  if (cmd.type === 'ACCEPT_EXISTING') return { mark: '●', label: 'Dùng video này' };
  if (cmd.type === 'ENSURE_MIX') return { mark: '●', label: 'Hoàn thiện Shot' };
  const label = DIRECTOR_SHOT_COMMAND_LABEL[cmd.type] || 'Cần xử lý';
  if (cmd.kind === 'WAIT') return { mark: '●', label };
  if (cmd.kind === 'BLOCK') return { mark: '○', label };
  return { mark: '●', label };
}

export function directorShotOpenLabel(index: number) {
  return `Mở Shot ${String(index + 1).padStart(2, '0')}`;
}

export type DirectorShotPhase = 'design' | 'picture' | 'motion' | 'finish';

export function directorShotPhase(cmd: ShotProductionCommand): DirectorShotPhase {
  if (
    cmd.type === 'ENSURE_VOICE' ||
    cmd.type === 'WAIT_VOICE' ||
    cmd.type === 'EDIT_INPUT' ||
    cmd.type === 'BLOCK'
  ) {
    return 'design';
  }
  if (
    cmd.type === 'ENSURE_PICTURE' ||
    cmd.type === 'WAIT_PICTURE' ||
    cmd.type === 'WAIT_PICTURE_APPROVAL'
  ) {
    return 'picture';
  }
  if (
    cmd.type === 'CONFIRM_MOTION' ||
    cmd.type === 'ACCEPT_EXISTING' ||
    cmd.type === 'WAIT_MOTION' ||
    cmd.type === 'WAIT_VIDEO_REVIEW' ||
    cmd.type === 'LIPSYNC_QA_REQUIRED'
  ) {
    return 'motion';
  }
  return 'finish';
}

export const DIRECTOR_SHOT_PHASES: { id: DirectorShotPhase; label: string }[] = [
  { id: 'design', label: 'Thiết kế' },
  { id: 'picture', label: 'Hình' },
  { id: 'motion', label: 'Chuyển động' },
  { id: 'finish', label: 'Hoàn thiện' },
];

export function directorPreviewCaption(kind: 'final' | 'video' | 'lipsync' | 'motion' | 'keyframe' | 'empty') {
  if (kind === 'empty') return 'Chưa có hình';
  if (kind === 'keyframe') return 'Hình';
  return 'Video';
}
