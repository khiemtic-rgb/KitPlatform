import {
  deriveDirectorWorkspace,
  directorShotTitle,
  shortShotLabel,
  shotSceneLabel,
  type DirectorWorkspaceInput,
} from './kit-video-director-workspace';

export const PRODUCTION_UX_ID = 'FAMIXA_PRODUCTION_UI_V1';

export const SERIES_STAFF_TABS = [
  { id: 'overview', label: 'Tổng quan' },
  { id: 'script', label: 'Kịch bản' },
  { id: 'scenes', label: 'Chia cảnh' },
  { id: 'characters', label: 'Nhân vật' },
  { id: 'voice', label: 'Thoại' },
  { id: 'images', label: 'Hình ảnh' },
  { id: 'video', label: 'Video' },
  { id: 'finish', label: 'Hoàn thiện' },
  { id: 'publish', label: 'Xuất bản' },
] as const;

export type SeriesStaffTab = (typeof SERIES_STAFF_TABS)[number]['id'];

export const SERIES_TASK_PANEL_ID: Record<SeriesStaffTab, string> = {
  overview: 'fx-tab-overview',
  script: 'fx-tab-script',
  scenes: 'fx-tab-scenes',
  characters: 'fx-tab-characters',
  voice: 'fx-tab-voice',
  images: 'fx-tab-images',
  video: 'fx-tab-video',
  finish: 'fx-tab-finish',
  publish: 'fx-tab-publish',
};

/** Same-tab next-work click must still move the page — setState(same tab) is a no-op. */
export function focusSeriesTaskPanel(tab: SeriesStaffTab) {
  const root = typeof document === 'undefined' ? null : document.getElementById(SERIES_TASK_PANEL_ID[tab]);
  const missing = root?.querySelector<HTMLElement>('[data-voice-gap="1"]') ?? null;
  (missing || root)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  missing?.focus?.();
}
export type ProductionUserMode = 'staff' | 'director';

export const SCENE_STEPS = [
  { id: 'content', label: 'Tổng quan cảnh' },
  { id: 'image', label: 'Hình ảnh' },
  { id: 'video', label: 'Video' },
] as const;

export type SceneStepId = (typeof SCENE_STEPS)[number]['id'];

export function sceneStepFromAction(action?: string): SceneStepId {
  if (action === 'DONE' || action === 'VIDEO_REVIEW') return 'video';
  if (
    action === 'CREATE_VIDEO_CONTRACT' ||
    action === 'APPROVE_VIDEO_CONTRACT' ||
    action === 'PREFLIGHT' ||
    action === 'EXECUTE' ||
    action === 'VIDEO_PROCESSING'
  ) {
    return 'video';
  }
  if (action === 'IMAGE_REVIEW' || action === 'IMAGE_REJECTED') return 'image';
  return 'content';
}

export const SERIES_TRACK = [
  { id: 'script', label: 'Kịch bản' },
  { id: 'scenes', label: 'Chia cảnh' },
  { id: 'cast', label: 'Nhân vật' },
  { id: 'voice', label: 'Thoại' },
  { id: 'image', label: 'Hình ảnh' },
  { id: 'video', label: 'Video' },
  { id: 'finish', label: 'Hoàn thiện' },
  { id: 'publish', label: 'Xuất bản' },
] as const;

export function deriveSeriesTrack(input: EpisodeProgressInput) {
  const shots = input.shotCount ?? input.shotTotal;
  const voiceDone = input.voiceReady !== false;
  const videosReady =
    input.shotTotal > 0 &&
    input.imagesMade >= input.shotTotal &&
    input.videosDone >= input.shotTotal;
  const finished = videosReady && input.finalReady !== false;
  const nodes = [
    { id: 'script' as const, done: input.scriptLocked, label: input.scriptLocked ? 'Hoàn thành' : 'Chưa xong' },
    {
      id: 'scenes' as const,
      done: input.shotGraphLocked || input.sceneCount > 0,
      label: input.sceneCount > 0 ? `${input.sceneCount} cảnh · ${shots} shot` : 'Chưa chia',
    },
    { id: 'cast' as const, done: input.characterLocked, label: input.characterLocked ? 'Hoàn thành' : input.characterNeed || 'Cần hoàn thiện' },
    { id: 'voice' as const, done: voiceDone, label: voiceDone ? 'Sẵn sàng' : 'Chưa gán thoại' },
    {
      id: 'image' as const,
      done: input.shotTotal > 0 && input.imagesMade >= input.shotTotal,
      label: input.shotTotal ? `${input.imagesMade}/${input.shotTotal}` : '0',
    },
    {
      id: 'video' as const,
      done: input.shotTotal > 0 && input.videosMade >= input.shotTotal,
      label: input.shotTotal ? `${input.videosMade}/${input.shotTotal}` : '0',
    },
    {
      id: 'finish' as const,
      done: videosReady,
      label: videosReady ? `${input.videosDone}/${input.shotTotal}` : 'Chưa sẵn sàng',
    },
    {
      id: 'publish' as const,
      done: finished,
      label: finished ? 'Sẵn sàng' : 'Chưa sẵn sàng',
    },
  ];
  const current = nodes.find((n) => !n.done)?.id || 'publish';
  return { nodes, current };
}

export const EPISODE_PIPELINE = [
  'SCRIPT',
  'BREAKDOWN',
  'LOOK',
  'LOCK',
  'PREP',
  'MAKE_IMAGE',
  'REVIEW_IMAGE',
  'MAKE_VIDEO',
  'REVIEW_VIDEO',
  'DONE',
] as const;

export type EpisodePipelineId = (typeof EPISODE_PIPELINE)[number];
export type UserTone = 'done' | 'review' | 'work' | 'wait' | 'block' | 'prep';

export type FriendlyStatus = {
  tone: UserTone;
  label: string;
  hint: string;
};

const up = (v?: string) => (v || '').toUpperCase();

export function friendlyStatus(raw?: string): FriendlyStatus {
  const s = up(raw);
  if (s === 'DRAFT') return { tone: 'prep', label: 'Đang chuẩn bị', hint: 'Cảnh đang được soạn.' };
  if (s === 'PENDING' || s === 'NOT_READY' || s === 'MISSING') {
    return { tone: 'wait', label: 'Đang chờ', hint: 'Chưa đến lượt xử lý.' };
  }
  if (s === 'READY_FOR_DIRECTOR') {
    return { tone: 'review', label: 'Đang chờ bạn duyệt', hint: 'Xem nội dung rồi quyết định.' };
  }
  if (s === 'DIRECTOR_APPROVED' || s === 'APPROVED' || s === 'IMAGE_APPROVED' || s === 'LOCKED') {
    return { tone: 'done', label: 'Đã duyệt', hint: 'Bước này đã xong.' };
  }
  if (s === 'BLOCKED') return { tone: 'block', label: 'Chưa thể thực hiện', hint: 'Cần hoàn tất bước trước.' };
  if (s === 'PROCESSING' || s === 'REQUESTED' || s === 'ACCEPTED') {
    return { tone: 'work', label: 'Đang xử lý', hint: 'Hệ thống đang làm việc.' };
  }
  if (s === 'SUCCEEDED' || s === 'COMPLETED') return { tone: 'done', label: 'Đã hoàn thành', hint: 'Cảnh đã xong.' };
  if (s === 'REJECTED' || s === 'IMAGE_REJECTED' || s === 'DIRECTOR_REJECTED') {
    return { tone: 'block', label: 'Đã từ chối', hint: 'Cần xử lý lại.' };
  }
  if (s === 'FAILED' || s === 'QA_FAILED') return { tone: 'block', label: 'Có lỗi cần xử lý', hint: 'Không tự thử lại.' };
  if (s === 'VALIDATED' || s === 'COMPILED') return { tone: 'prep', label: 'Đang chuẩn bị', hint: 'Sắp đến bước duyệt.' };
  return { tone: 'wait', label: 'Chưa bắt đầu', hint: 'Chưa có việc ở bước này.' };
}

export function stepperUserCaption(status: string, current: boolean) {
  if (current && (status === 'READY FOR REVIEW' || status === 'READY_FOR_DIRECTOR')) return 'Đang chờ duyệt';
  if (status === 'LOCKED' || status === 'APPROVED' || status === 'COMPLETED') return 'Hoàn thành';
  if (status === 'READY FOR REVIEW') return 'Đang chờ duyệt';
  if (status === 'BLOCKED') return 'Cần xử lý';
  return 'Chưa bắt đầu';
}

export function shotUserLabel(input: DirectorWorkspaceInput) {
  const d = deriveDirectorWorkspace(input);
  if (d.action === 'DONE') {
    return { tone: 'done' as UserTone, label: 'Hoàn thành', hint: 'Cảnh đã xong.', action: 'Xem lại cảnh', urgent: false };
  }
  if (d.action === 'IMAGE_REVIEW') {
    return {
      tone: 'review' as UserTone,
      label: 'Chờ duyệt',
      hint: 'Mở cảnh, xem ảnh lớn, rồi chọn Duyệt ảnh hoặc Yêu cầu làm lại.',
      action: 'Xem và duyệt ảnh',
      urgent: true,
    };
  }
  if (d.action === 'IMAGE_REJECTED') {
    return { tone: 'block' as UserTone, label: 'Cần xử lý', hint: 'Ảnh không đạt.', action: 'Xem cảnh', urgent: true };
  }
  if (d.action === 'VIDEO_REVIEW') {
    return {
      tone: 'review' as UserTone,
      label: 'Chờ duyệt',
      hint: 'Xem video rồi quyết định.',
      action: 'Xem và duyệt video',
      urgent: true,
    };
  }
  if (d.action === 'CREATE_VIDEO_CONTRACT' || d.action === 'APPROVE_VIDEO_CONTRACT' || d.action === 'PREFLIGHT' || d.action === 'EXECUTE') {
    return {
      tone: 'work' as UserTone,
      label: 'Đang chuẩn bị video',
      hint: 'Ảnh đã duyệt. Vào cảnh để chuẩn bị video — máy không tự tạo.',
      action: 'Đi tới bước video',
      urgent: true,
    };
  }
  if (d.current === 'IMAGE' && d.steps.IMAGE === 'WAITING') {
    return { tone: 'wait' as UserTone, label: 'Chưa bắt đầu', hint: 'Ảnh của cảnh chưa có.', action: 'Mở cảnh', urgent: false };
  }
  return { tone: 'wait' as UserTone, label: 'Chưa bắt đầu', hint: 'Cảnh chưa vào bước làm việc.', action: 'Mở cảnh', urgent: false };
}

export function sceneBrief(spec?: Record<string, unknown> | null, note?: string | null, characterName?: string) {
  const scene = spec?.scene && typeof spec.scene === 'object' ? (spec.scene as Record<string, unknown>) : {};
  const pick = (...xs: unknown[]) => {
    for (const x of xs) {
      if (typeof x === 'string' && x.trim()) return x.trim();
      if (Array.isArray(x)) {
        const joined = x.filter((v) => typeof v === 'string' && v.trim()).join(', ');
        if (joined) return joined;
      }
    }
    return '';
  };
  return {
    title: directorShotTitle(spec, note),
    scene: pick(scene.summary, scene.description, scene.intent, spec?.note, note),
    characters: pick(scene.characters, spec?.characterName, characterName),
    setting: pick(scene.location, scene.setting, spec?.location, spec?.setting),
    action: pick(scene.action, spec?.engineAction, spec?.action),
    emotion: pick(scene.emotion, scene.mood, spec?.emotion, spec?.mood),
  };
}

export type EpisodeProgressInput = {
  scriptLocked: boolean;
  sceneCount: number;
  shotCount?: number;
  shotGraphLocked: boolean;
  characterLocked: boolean;
  characterNeed?: string;
  prepDone: number;
  shotTotal: number;
  imagesMade: number;
  imagesReviewed: number;
  videosMade: number;
  videosReviewed: number;
  videosDone: number;
  voiceReady?: boolean;
  finalReady?: boolean;
};

export function episodePipelineName(id: EpisodePipelineId) {
  if (id === 'SCRIPT') return 'Kịch bản';
  if (id === 'BREAKDOWN') return 'Chia cảnh';
  if (id === 'LOOK') return 'Tạo hình nhân vật';
  if (id === 'LOCK') return 'Khóa nhân vật';
  if (id === 'PREP') return 'Chuẩn bị cảnh';
  if (id === 'MAKE_IMAGE') return 'Tạo ảnh';
  if (id === 'REVIEW_IMAGE') return 'Duyệt ảnh';
  if (id === 'MAKE_VIDEO') return 'Tạo video';
  if (id === 'REVIEW_VIDEO') return 'Duyệt video';
  return 'Hoàn thành';
}

export function deriveEpisodePipeline(input: EpisodeProgressInput) {
  const total = Math.max(input.shotTotal, 0);
  const nodes = [
    { id: 'SCRIPT' as const, done: input.scriptLocked, detail: input.scriptLocked ? 'Đã hoàn thành' : 'Chưa khóa' },
    {
      id: 'BREAKDOWN' as const,
      done: input.shotGraphLocked || input.sceneCount > 0,
      detail: input.sceneCount > 0 ? `${input.sceneCount} cảnh` : 'Chưa chia',
    },
    { id: 'LOOK' as const, done: input.characterLocked, detail: input.characterLocked ? 'Đã có hình' : 'Chưa xong' },
    { id: 'LOCK' as const, done: input.characterLocked, detail: input.characterLocked ? 'Đã khóa' : 'Chưa khóa' },
    {
      id: 'PREP' as const,
      done: total > 0 && input.prepDone >= total,
      detail: total ? `${input.prepDone}/${total} cảnh` : 'Chưa có cảnh production',
    },
    {
      id: 'MAKE_IMAGE' as const,
      done: total > 0 && input.imagesMade >= total,
      detail: total ? `${input.imagesMade}/${total}` : '0',
    },
    {
      id: 'REVIEW_IMAGE' as const,
      done: total > 0 && input.imagesReviewed >= total,
      detail: total ? `${input.imagesReviewed}/${Math.max(input.imagesMade, total)}` : '0',
    },
    {
      id: 'MAKE_VIDEO' as const,
      done: total > 0 && input.videosMade >= total,
      detail: total ? `${input.videosMade}/${total}` : '0',
    },
    {
      id: 'REVIEW_VIDEO' as const,
      done: total > 0 && input.videosReviewed >= total,
      detail: total ? `${input.videosReviewed}/${total}` : '0',
    },
    {
      id: 'DONE' as const,
      done: total > 0 && input.videosDone >= total,
      detail: total ? `${input.videosDone}/${total}` : '0',
    },
  ];
  const current = nodes.find((n) => !n.done)?.id || 'DONE';
  const finished = nodes.filter((n) => n.done).length;
  const percent = Math.round((finished / nodes.length) * 100);
  return { nodes, current, percent, finished, total: nodes.length };
}

export function countPrepDone(shotContract?: string) {
  return up(shotContract) === 'DIRECTOR_APPROVED';
}

export function countImageMade(executionStatus?: string, directorApproval?: string) {
  const s = up(executionStatus);
  return s === 'READY_FOR_DIRECTOR' || s === 'IMAGE_APPROVED' || up(directorApproval) === 'APPROVED';
}

export function countImageReviewed(directorApproval?: string) {
  return up(directorApproval) === 'APPROVED';
}

export function countVideoMade(status?: string) {
  return ['READY_FOR_DIRECTOR', 'DIRECTOR_APPROVED', 'SUCCEEDED', 'PROCESSING'].includes(up(status));
}

export function countVideoReviewed(status?: string) {
  return up(status) === 'DIRECTOR_APPROVED';
}

export function staffName(name?: string | null) {
  const v = (name || '').trim();
  return v || 'Nhân vật';
}

export function productionShotHeading(shotCode?: string, title?: string) {
  const code = shortShotLabel(shotCode);
  const scene = shotSceneLabel(shotCode);
  return { code, scene, title: (title || '').trim() };
}

export function episodeWorkStatus(input: { shotTotal: number; videosDone: number }) {
  if (input.shotTotal > 0 && input.videosDone >= input.shotTotal) {
    return { label: 'Đã hoàn thành', tone: 'done' as const };
  }
  return { label: 'Đang sản xuất', tone: 'work' as const };
}

export function nextWorkCopy(opts: {
  scriptLocked: boolean;
  sceneCount: number;
  characterLocked: boolean;
  characterNeed?: string;
  action?: string;
  sceneLabel: string;
  done: boolean;
  imagesPending?: boolean;
  videosPending?: boolean;
  videosReviewPending?: boolean;
  voicePending?: boolean;
  lipsyncPending?: boolean;
  finishReady?: boolean;
}) {
  if (!opts.scriptLocked) {
    return {
      headline: 'Hoàn thiện kịch bản',
      hint: 'Xem và xác nhận kịch bản trước khi chia cảnh.',
      action: 'Mở kịch bản',
      tab: 'script' as const,
    };
  }
  if (opts.sceneCount <= 0) {
    return {
      headline: 'Chia video thành các cảnh',
      hint: 'Kịch bản đã có. Hãy chia thành các cảnh trước khi làm hình.',
      action: 'Chia cảnh',
      tab: 'scenes' as const,
    };
  }
  if (opts.imagesPending && !opts.characterLocked) {
    return {
      headline: opts.characterNeed || 'Hoàn thiện bộ ảnh chuẩn',
      hint: 'Nhân vật cần bộ ảnh chuẩn đủ 4 góc trước khi tạo hình.',
      action: 'Xem nhân vật',
      tab: 'characters' as const,
    };
  }
  if (opts.imagesPending) {
    return {
      headline: 'Tạo hình ảnh cho các cảnh.',
      hint: 'Đây là hình của bản dựng đang mở, không phải cảnh production dùng chung.',
      action: 'Bắt đầu tạo hình',
      tab: 'images' as const,
    };
  }
  if (opts.videosPending) {
    return {
      headline: 'Tạo video cho các cảnh.',
      hint: 'Đây là video của bản dựng đang mở.',
      action: 'Sang tạo video',
      tab: 'video' as const,
    };
  }
  if (opts.videosReviewPending) {
    return {
      headline: 'Duyệt video các cảnh.',
      hint: 'Xem clip rồi chọn Duyệt video hoặc Không đạt. Take Runway vẫn câm.',
      action: 'Xem và duyệt video',
      tab: 'video' as const,
    };
  }
  if (opts.voicePending) {
    return {
      headline: 'Gán thoại / Voice trước khi hoàn thiện.',
      hint: 'Shot có lời cần Voice Asset + duration. Không overlay TTS lên take câm để gọi là Final.',
      action: 'Mở thoại',
      tab: 'voice' as const,
    };
  }
  if (opts.lipsyncPending) {
    return {
      headline: 'Chờ Lip-sync',
      hint: 'Hình ảnh đã tạo. Shot có thoại chưa khớp môi — chưa phải Final.',
      action: 'Xem lip-sync',
      tab: 'finish' as const,
    };
  }
  if (opts.action === 'IMAGE_REVIEW') {
    return {
      headline: `${opts.sceneLabel} đang chờ duyệt hình ảnh.`,
      hint: 'Xem ảnh lớn rồi chọn Duyệt ảnh hoặc Yêu cầu làm lại.',
      action: 'Xem và duyệt ảnh',
      tab: 'images' as const,
    };
  }
  if (opts.action === 'VIDEO_REVIEW') {
    return {
      headline: `${opts.sceneLabel} đang chờ duyệt video.`,
      hint: 'Xem video rồi quyết định.',
      action: 'Xem và duyệt video',
      tab: 'video' as const,
    };
  }
  if (
    opts.action === 'CREATE_VIDEO_CONTRACT' ||
    opts.action === 'APPROVE_VIDEO_CONTRACT' ||
    opts.action === 'PREFLIGHT' ||
    opts.action === 'EXECUTE'
  ) {
    return {
      headline: `${opts.sceneLabel} đã sẵn sàng tạo video.`,
      hint: 'Ảnh cần được duyệt trước đã xong. Mở cảnh để tạo video.',
      action: 'Tạo video',
      tab: 'video' as const,
    };
  }
  if (opts.finishReady && !opts.done) {
    return {
      headline: 'Hoàn thiện tập.',
      hint: 'Mọi cảnh đã duyệt video. Xem lại rồi xuất bản.',
      action: 'Sang hoàn thiện',
      tab: 'finish' as const,
    };
  }
  if (opts.done) {
    return {
      headline: 'Xuất bản video',
      hint: 'Mọi cảnh đã có video. Xem lại rồi xuất bản.',
      action: 'Sang xuất bản',
      tab: 'publish' as const,
    };
  }
  return {
    headline: `${opts.sceneLabel} cần xử lý.`,
    hint: 'Mở cảnh để xem bước tiếp theo.',
    action: 'Xem cảnh',
    tab: 'scenes' as const,
  };
}

export function sceneListStatus(action?: string) {
  if (action === 'DONE') return { mark: '✓', label: 'Hoàn thành' };
  if (
    action === 'IMAGE_REVIEW' ||
    action === 'VIDEO_REVIEW' ||
    action === 'EXECUTE' ||
    action === 'VIDEO_PROCESSING' ||
    action === 'CREATE_VIDEO_CONTRACT' ||
    action === 'APPROVE_VIDEO_CONTRACT' ||
    action === 'PREFLIGHT'
  ) {
    return { mark: '●', label: 'Đang thực hiện' };
  }
  if (action === 'IMAGE_REJECTED') return { mark: '⚠', label: 'Cần xử lý' };
  return { mark: '○', label: 'Chưa thực hiện' };
}

export function imageBoardStatus(action?: string, hasStill?: boolean) {
  if (action === 'IMAGE_REVIEW') return { mark: '🟡', label: 'Chờ duyệt', action: 'Xem ảnh' };
  if (action === 'IMAGE_REJECTED') return { mark: '↻', label: 'Cần làm lại', action: 'Xem ảnh' };
  if (
    action === 'CREATE_VIDEO_CONTRACT' ||
    action === 'APPROVE_VIDEO_CONTRACT' ||
    action === 'PREFLIGHT' ||
    action === 'EXECUTE' ||
    action === 'VIDEO_REVIEW' ||
    action === 'VIDEO_PROCESSING' ||
    action === 'DONE'
  ) {
    return { mark: '✓', label: 'Ảnh đã duyệt', action: 'Xem cảnh' };
  }
  if (!hasStill) return { mark: '○', label: 'Chưa thực hiện', action: 'Tạo ảnh' };
  return { mark: '○', label: 'Chưa thực hiện', action: 'Xem ảnh' };
}

export function videoBoardStatus(action?: string, hasClip?: boolean) {
  if (action === 'VIDEO_REVIEW') return { mark: '🟡', label: 'Chờ duyệt', action: 'Xem và duyệt video' };
  if (action === 'DONE') return { mark: '✓', label: 'Video đã duyệt', action: 'Xem video' };
  if (action === 'VIDEO_PROCESSING') return { mark: '●', label: 'Đang tạo video', action: 'Xem cảnh' };
  if (hasClip) return { mark: '●', label: 'Đang thực hiện', action: 'Xem video' };
  if (
    action === 'CREATE_VIDEO_CONTRACT' ||
    action === 'APPROVE_VIDEO_CONTRACT' ||
    action === 'PREFLIGHT' ||
    action === 'EXECUTE'
  ) {
    return { mark: '○', label: 'Chưa thực hiện', action: 'Tạo video' };
  }
  return { mark: '○', label: 'Chưa thực hiện', action: 'Xem cảnh' };
}

export function sceneChecklist(input: DirectorWorkspaceInput) {
  const d = deriveDirectorWorkspace(input);
  return [
    { id: 'cast', label: 'Nhân vật', done: d.steps.CHARACTER === 'LOCKED' },
    { id: 'image', label: 'Hình ảnh', done: d.imageOk || d.action === 'IMAGE_REVIEW' },
    { id: 'imageReview', label: 'Duyệt ảnh', done: d.imageOk },
    { id: 'video', label: 'Video', done: d.action === 'VIDEO_REVIEW' || d.action === 'DONE' || d.action === 'VIDEO_PROCESSING' },
    { id: 'videoReview', label: 'Duyệt video', done: d.action === 'DONE' },
    { id: 'done', label: 'Hoàn thành', done: d.action === 'DONE' },
  ];
}

/** Director-facing labels for nextShotProductionCommand(). Does not invent commands. */
export const DIRECTOR_SHOT_COMMAND_LABEL: Record<string, string> = {
  ENSURE_VOICE: 'Tạo thoại',
  WAIT_VOICE: 'Đang tạo thoại...',
  ENSURE_PICTURE: 'Tạo hình',
  WAIT_PICTURE: 'Đang tạo hình...',
  WAIT_PICTURE_APPROVAL: 'Duyệt hình',
  CONFIRM_MOTION: 'Tạo video',
  ACCEPT_EXISTING: 'Dùng video này',
  EDIT_INPUT: 'Đổi diễn',
  WAIT_MOTION: 'Đang tạo video',
  WAIT_VIDEO_REVIEW: 'Duyệt video',
  LIPSYNC_QA_REQUIRED: 'Kiểm tra video',
  CONFIRM_LIPSYNC: 'Lồng tiếng',
  WAIT_LIPSYNC: 'Đang tạo Lip-sync',
  ENSURE_MIX: 'Hoàn thiện Shot',
  WAIT_MIX: 'Đang hoàn thiện',
  READY_FINAL: 'Xem video',
  BLOCK: 'Chưa thể tiếp tục',
};

/** OR-merge Director QA ticks. Later patches must not drop voiceFace/action/continuity. */
export function mergeCanonicalShotQa(
  ...parts: Array<
    | {
        action?: boolean;
        continuity?: boolean;
        motion?: boolean;
        dialogue?: boolean;
        lipsync?: boolean;
        lipsyncQuality?: boolean;
        finalAv?: boolean;
        voiceFace?: boolean;
      }
    | undefined
  >
) {
  const out: {
    action?: boolean;
    continuity?: boolean;
    motion?: boolean;
    dialogue?: boolean;
    lipsync?: boolean;
    lipsyncQuality?: boolean;
    finalAv?: boolean;
    voiceFace?: boolean;
  } = {};
  for (const part of parts) {
    if (!part) continue;
    if (part.action) out.action = true;
    if (part.continuity) out.continuity = true;
    if (part.motion) out.motion = true;
    if (part.dialogue) out.dialogue = true;
    if (part.lipsync) out.lipsync = true;
    if (part.lipsyncQuality) out.lipsyncQuality = true;
    if (part.finalAv) out.finalAv = true;
    if (part.voiceFace) out.voiceFace = true;
  }
  return out;
}

export function directorPictureHoldCopy(blocker?: { code?: string; title?: string; type?: string }) {
  if (!blocker) {
    return { reason: 'Chưa thể tạo hình', hint: 'Shot chưa đủ thông tin.' };
  }
  if (blocker.code === 'VOICE_NOT_LOCKED' || blocker.type === 'voice') {
    return {
      reason: 'Thoại của Episode chưa được khóa.',
      hint: 'Hoàn tất và khóa thoại trước khi tạo hình.',
    };
  }
  if (blocker.code === 'SCENE_MASTER_NOT_LOCKED' || blocker.type === 'scene_master') {
    return { reason: 'Chưa thể tạo hình', hint: 'Scene Master chưa sẵn sàng.' };
  }
  if (blocker.code === 'SHOT_GRAPH_NOT_LOCKED' || blocker.type === 'shot_graph') {
    return { reason: 'Chưa thể tạo hình', hint: 'Shot chưa đủ thông tin.' };
  }
  if (blocker.code === 'SCRIPT_NOT_LOCKED' || blocker.type === 'script') {
    return { reason: 'Chưa thể tạo hình', hint: 'Kịch bản chưa khóa.' };
  }
  return { reason: 'Chưa thể tạo hình', hint: blocker.title || 'Shot chưa đủ thông tin.' };
}
