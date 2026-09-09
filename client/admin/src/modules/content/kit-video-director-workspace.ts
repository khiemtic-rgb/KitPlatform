export const DIRECTOR_PRODUCTION_WORKSPACE_ID = 'PRODUCTION_DIRECTOR_WORKSPACE_UI_V1';

export const DIRECTOR_PIPELINE = ['CHARACTER', 'SHOT', 'IMAGE', 'VIDEO', 'FINAL'] as const;
export type DirectorStepId = (typeof DIRECTOR_PIPELINE)[number];
export type DirectorStepStatus = 'LOCKED' | 'APPROVED' | 'READY FOR REVIEW' | 'WAITING' | 'BLOCKED' | 'COMPLETED';
export type DirectorActionKind =
  | 'IMAGE_REVIEW'
  | 'IMAGE_REJECTED'
  | 'CREATE_VIDEO_CONTRACT'
  | 'APPROVE_VIDEO_CONTRACT'
  | 'PREFLIGHT'
  | 'EXECUTE'
  | 'VIDEO_PROCESSING'
  | 'VIDEO_REVIEW'
  | 'DONE'
  | 'WAIT';

export type DirectorWorkspaceInput = {
  master?: string;
  dna?: string;
  prp?: string;
  shotContract?: string;
  executionStatus?: string;
  directorApproval?: string;
  stillApproved?: boolean;
  videoContractStatus?: string;
  videoContractId?: string | null;
  videoGenerationStatus?: string;
  videoPreflightPass?: boolean;
  videoProviderRequestId?: string | null;
  videoRequestedAt?: string | null;
};

const up = (v?: string) => (v || '').toUpperCase();
const isLocked = (v?: string) => up(v) === 'LOCKED';
const isDirectorApproved = (v?: string) => up(v) === 'DIRECTOR_APPROVED';
const ORPHAN_MS = 3 * 60 * 1000;

export function isOrphanedVideoRequest(
  status?: string,
  providerRequestId?: string | null,
  requestedAt?: string | null,
  now = Date.now(),
) {
  const vg = up(status);
  if (!['REQUESTED', 'ACCEPTED', 'PROCESSING'].includes(vg)) return false;
  if ((providerRequestId || '').trim()) return false;
  if (!requestedAt) return vg === 'REQUESTED';
  const at = Date.parse(requestedAt);
  return Number.isFinite(at) && now - at >= ORPHAN_MS;
}

export function shortShotLabel(shotCode?: string) {
  const code = (shotCode || '').toUpperCase();
  return code.match(/SHOT-\d+/)?.[0] || 'SHOT-001';
}

export function shotDisplayName(shotCode?: string) {
  return shortShotLabel(shotCode).replace('-', ' ');
}

export function directorStatusLabel(raw?: string) {
  const s = up(raw);
  if (s === 'DRAFT') return 'Draft';
  if (s === 'VALIDATED') return 'Validated';
  if (s === 'DIRECTOR_APPROVED' || s === 'APPROVED' || s === 'IMAGE_APPROVED') return 'Approved';
  if (s === 'READY_FOR_DIRECTOR') return 'Ready for Director';
  if (s === 'PENDING' || s === 'NOT_READY' || s === 'MISSING') return 'Waiting';
  if (s === 'BLOCKED') return 'Blocked';
  if (s === 'REJECTED' || s === 'IMAGE_REJECTED' || s === 'DIRECTOR_REJECTED') return 'Rejected';
  if (s === 'COMPILED' || s === 'LOCKED') return 'Ready';
  if (s === 'SUPERSEDED') return 'Superseded';
  if (s === 'SUCCEEDED' || s === 'COMPLETED') return 'Completed';
  if (s === 'PROCESSING' || s === 'REQUESTED' || s === 'ACCEPTED') return 'Processing';
  return raw ? raw.replace(/_/g, ' ') : 'Waiting';
}

export function stepperMark(status: DirectorStepStatus, current: boolean) {
  if (current) return '●';
  if (status === 'LOCKED' || status === 'APPROVED' || status === 'COMPLETED') return '✓';
  return '○';
}

export function stepperName(id: DirectorStepId) {
  if (id === 'CHARACTER') return 'Nhân vật';
  if (id === 'SHOT') return 'Cảnh';
  if (id === 'IMAGE') return 'Ảnh';
  if (id === 'VIDEO') return 'Video';
  return 'Xong';
}

export function shotSceneLabel(shotCode?: string) {
  const n = (shotCode || '').match(/SHOT-(\d+)/)?.[1];
  return n ? `Cảnh ${Number(n)}` : 'Cảnh';
}

export function stepperCaption(id: DirectorStepId, status: DirectorStepStatus, current: boolean) {
  if (id === 'CHARACTER' && status === 'LOCKED') return 'LOCKED';
  if (id === 'SHOT' && status === 'APPROVED') return 'APPROVED';
  if (id === 'IMAGE' && (status === 'READY FOR REVIEW' || current)) return 'ĐANG DUYỆT';
  if (id === 'IMAGE' && status === 'APPROVED') return 'APPROVED';
  if (status === 'BLOCKED') return 'BLOCKED';
  if (status === 'COMPLETED') return 'DONE';
  if (status === 'WAITING') return 'WAITING';
  return directorStatusLabel(status === 'READY FOR REVIEW' ? 'READY_FOR_DIRECTOR' : status);
}

export function truncateSha(sha?: string) {
  const v = (sha || '').trim();
  if (v.length < 16) return '—';
  return `${v.slice(0, 8)}...${v.slice(-6)}`;
}

export function directorShotTitle(spec?: Record<string, unknown> | null, note?: string | null) {
  const scene = spec?.scene && typeof spec.scene === 'object' ? (spec.scene as Record<string, unknown>) : {};
  const candidates = [spec?.engineAction, scene.action, scene.title, scene.intent, spec?.note, note];
  for (const raw of candidates) {
    if (typeof raw === 'string' && raw.trim()) return raw.trim();
  }
  return '';
}

export function displayShotTitle(spec?: Record<string, unknown> | null, note?: string | null, fallback = '') {
  const raw = directorShotTitle(spec, note).trim();
  if (!raw) return fallback;
  if (/^(SPEC|FORMAT|HOLD|INSERT|NỘI\.?|SHOT(-\d+)?)$/i.test(raw)) return fallback;
  if (raw.length <= 16 && raw === raw.toUpperCase() && /^[A-Z0-9 ._-]+$/.test(raw)) return fallback;
  return raw;
}

export function qaPassedCount(qa?: { technical?: string; character?: string; identity?: string; continuity?: string; composition?: string } | null) {
  const keys = ['technical', 'character', 'identity', 'continuity', 'composition'] as const;
  const passed = keys.filter((k) => up(qa?.[k]) === 'PASS').length;
  return { passed, total: keys.length, all: passed === keys.length };
}

export function deriveDirectorWorkspace(input: DirectorWorkspaceInput) {
  const character: DirectorStepStatus =
    isLocked(input.master) && isLocked(input.dna) && isLocked(input.prp) ? 'LOCKED' : 'WAITING';
  const shot: DirectorStepStatus = isDirectorApproved(input.shotContract) ? 'APPROVED' : 'WAITING';

  const approval = up(input.directorApproval);
  const exec = up(input.executionStatus);
  let image: DirectorStepStatus = 'WAITING';
  if (approval === 'REJECTED' || exec === 'IMAGE_REJECTED') image = 'BLOCKED';
  else if (approval === 'APPROVED' || exec === 'IMAGE_APPROVED') image = 'APPROVED';
  else if (exec === 'READY_FOR_DIRECTOR') image = 'READY FOR REVIEW';

  const imageOk = image === 'APPROVED' || input.stillApproved === true;
  const vc = up(input.videoContractStatus);
  const vg = up(input.videoGenerationStatus);

  let video: DirectorStepStatus = 'WAITING';
  if (approval === 'REJECTED') video = 'WAITING';
  else if (vg === 'DIRECTOR_APPROVED') video = 'COMPLETED';
  else if (vg === 'READY_FOR_DIRECTOR') video = 'READY FOR REVIEW';
  else if (isDirectorApproved(vc)) video = 'APPROVED';
  else if (vc === 'VALIDATED') video = 'READY FOR REVIEW';
  else if (vc === 'REJECTED') video = 'BLOCKED';
  else if (imageOk) video = 'WAITING';

  const final: DirectorStepStatus = vg === 'DIRECTOR_APPROVED' ? 'COMPLETED' : 'WAITING';

  let current: DirectorStepId = 'CHARACTER';
  if (character === 'LOCKED') current = shot === 'APPROVED' ? 'IMAGE' : 'SHOT';
  if (imageOk) current = 'VIDEO';
  if (vg === 'DIRECTOR_APPROVED') current = 'FINAL';
  if (image === 'READY FOR REVIEW' || image === 'BLOCKED') current = 'IMAGE';

  let action: DirectorActionKind = 'WAIT';
  let currentActionTitle = 'Chưa đến lượt';
  let currentActionBody = 'Chưa có việc Director cần làm.';
  let next = 'Xem và duyệt ảnh';
  if (image === 'READY FOR REVIEW') {
    action = 'IMAGE_REVIEW';
    currentActionTitle = 'Xem và duyệt ảnh';
    currentActionBody = 'Bạn hãy xem ảnh và quyết định có sử dụng ảnh này hay không.';
    next = 'Chuẩn bị video cho cảnh này';
  } else if (image === 'BLOCKED') {
    action = 'IMAGE_REJECTED';
    currentActionTitle = 'Ảnh không đạt';
    currentActionBody = 'Ảnh này không dùng để làm video.';
    next = 'Ảnh không đạt — dừng. Không tạo hợp đồng video.';
  } else if (imageOk && !input.videoContractId && (vc === '' || vc === 'NOT_READY' || vc === 'MISSING' || vc === 'DRAFT')) {
    action = 'CREATE_VIDEO_CONTRACT';
    currentActionTitle = 'Chuẩn bị video';
    currentActionBody = 'Ảnh đã được duyệt. Bước tiếp theo: chuẩn bị video cho cảnh này.';
    next = 'Chuẩn bị video cho cảnh này';
  } else if (imageOk && !isDirectorApproved(vc)) {
    action = 'APPROVE_VIDEO_CONTRACT';
    currentActionTitle = 'Duyệt kế hoạch video';
    currentActionBody = 'Kế hoạch video còn bản nháp cho đến khi bạn duyệt. Bước này không tạo video.';
    next = 'Duyệt kế hoạch video';
  } else if (isDirectorApproved(vc) && vg === 'READY_FOR_DIRECTOR') {
    action = 'VIDEO_REVIEW';
    currentActionTitle = 'Xem video production';
    currentActionBody = 'Video đang chờ bạn duyệt. Máy không tự duyệt.';
    next = 'Đồng ý hoặc không đạt video này.';
  } else if (isOrphanedVideoRequest(vg, input.videoProviderRequestId, input.videoRequestedAt)) {
    action = 'EXECUTE';
    currentActionTitle = 'Lần tạo trước bị đứt';
    currentActionBody = 'Chưa có video. Tạo đúng một lần.';
    next = 'Tạo lại một lần';
  } else if (['REQUESTED', 'ACCEPTED', 'PROCESSING'].includes(vg)) {
    action = 'VIDEO_PROCESSING';
    currentActionTitle = 'Đang tạo video';
    currentActionBody = 'Nhà cung cấp đang làm video. Không bấm tạo lại.';
    next = 'Chờ video xong rồi duyệt';
  } else if (
    isDirectorApproved(vc) &&
    input.videoPreflightPass &&
    !['REQUESTED', 'ACCEPTED', 'PROCESSING', 'SUCCEEDED', 'READY_FOR_DIRECTOR', 'DIRECTOR_APPROVED', 'DIRECTOR_REJECTED', 'FAILED', 'QA_FAILED'].includes(vg)
  ) {
    action = 'EXECUTE';
    currentActionTitle = 'Sẵn sàng tạo video';
    currentActionBody = 'Bước này sẽ gọi nhà cung cấp và tạo đúng một video.';
    next = 'Tạo video';
  } else if (isDirectorApproved(vc) && !input.videoPreflightPass) {
    action = 'PREFLIGHT';
    currentActionTitle = 'Kiểm tra điều kiện';
    currentActionBody = 'Cảnh chưa đủ điều kiện để tạo video nếu bước này chưa đạt. Chưa gọi nhà cung cấp.';
    next = 'Kiểm tra điều kiện';
  } else if (vg === 'DIRECTOR_APPROVED') {
    action = 'DONE';
    currentActionTitle = 'Production đã xong';
    currentActionBody = 'Bạn đã duyệt video production.';
    next = 'Hoàn tất';
  }

  const currentStage =
    action === 'IMAGE_REVIEW'
      ? 'IMAGE · DIRECTOR REVIEW REQUIRED'
      : action === 'IMAGE_REJECTED'
        ? 'IMAGE · REJECTED'
        : action === 'CREATE_VIDEO_CONTRACT' || action === 'APPROVE_VIDEO_CONTRACT'
          ? 'VIDEO · VIDEO CONTRACT'
          : action === 'PREFLIGHT' || action === 'EXECUTE' || action === 'VIDEO_PROCESSING' || action === 'VIDEO_REVIEW'
            ? 'VIDEO · GENERATION'
            : action === 'DONE'
              ? 'FINAL · COMPLETE'
              : `${current} · ${directorStatusLabel(current)}`;

  return {
    steps: { CHARACTER: character, SHOT: shot, IMAGE: image, VIDEO: video, FINAL: final },
    current,
    action,
    currentActionTitle,
    currentActionBody,
    currentStage,
    next,
    showVideoGeneration: imageOk && isDirectorApproved(vc),
    imageOk,
  };
}
